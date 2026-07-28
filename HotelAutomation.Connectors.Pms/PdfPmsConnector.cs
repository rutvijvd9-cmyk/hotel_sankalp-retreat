namespace HotelAutomation.Connectors.Pms;

using System.Security.Cryptography;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// IPmsEventSource implementation that watches a folder for PDF files
/// produced by a virtual printer (or placed manually) and converts them
/// into PmsEvents that feed the WhatsApp automation pipeline.
///
/// PHASE 1 — Manual drop mode:
///   Watches WatchFolder for new *.pdf files (FileSystemWatcher Created events).
///   Each new file is extracted and archived.
///
/// PHASE 2 — Virtual printer mode:
///   "Hotel Sankalp Printer" always overwrites the same fixed file (PrinterOutputFile).
///   The connector listens for Changed events on that specific file.
///   SHA-256 content hashing prevents the same print job from being processed twice
///   if multiple change events fire for one write.
///
/// CONFIGURATION (appsettings.json "PdfBridge" section):
///   WatchFolder       — folder to monitor for new PDFs  (default: C:\HotelPrints\Output)
///   PrinterOutputFile — fixed file written by the virtual printer (Phase 2)
///   ArchiveFolder     — processed PDFs go here           (default: C:\HotelPrints\Processed)
///   ArchiveProcessed  — move after processing?           (default: true)
///   DefaultEventType  — "CheckOut" or "CheckIn"          (default: "CheckOut")
///   FileReadDelayMs   — ms to wait before reading file   (default: 800)
/// </summary>
public sealed class PdfPmsConnector : IPmsEventSource
{
    private readonly PdfDataExtractor _extractor;
    private readonly PdfBridgeOptions _options;
    private readonly ILogger<PdfPmsConnector> _logger;

    // Two watchers: one for new files in the folder, one for changes to the fixed printer file
    private FileSystemWatcher? _folderWatcher;
    private FileSystemWatcher? _printerFileWatcher;

    // Hash of the last processed PrinterOutputFile content — prevents double-processing
    // a single print job when Windows fires multiple Changed events for one write.
    private string? _lastPrinterFileHash;
    private readonly object _hashLock = new();

    // Statistics exposed via the /api/pdf-bridge/status endpoint
    public int FilesProcessed { get; private set; }
    public int FilesFailedExtraction { get; private set; }
    public string? LastProcessedFile { get; private set; }
    public DateTimeOffset? LastProcessedAt { get; private set; }
    public string? LastError { get; private set; }

    public Func<Task<string?>>? GetActiveUserSignaturePath { get; set; }

    /// <summary>Absolute path to the writable signatures directory (AppData). Set by Program.cs at startup.</summary>
    public string? SignaturesDataDir { get; set; }

    public event EventHandler<PmsEvent>? EventReceived;

    public PdfPmsConnector(
        PdfDataExtractor extractor,
        PdfBridgeOptions options,
        ILogger<PdfPmsConnector> logger)
    {
        _extractor = extractor;
        _options   = options;
        _logger    = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // ── Resolve {AppDir} → the directory containing the running executable ──
        // This allows appsettings.json to use "{AppDir}\\PDFs" and have it
        // automatically resolve to e.g. C:\Program Files\Scalifts\PDFs
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _options.WatchFolder       = _options.WatchFolder.Replace("{AppDir}", appDir, StringComparison.OrdinalIgnoreCase);
        _options.PrinterOutputFile = _options.PrinterOutputFile.Replace("{AppDir}", appDir, StringComparison.OrdinalIgnoreCase);
        _options.ArchiveFolder     = _options.ArchiveFolder.Replace("{AppDir}", appDir, StringComparison.OrdinalIgnoreCase);

        // Normalize paths for macOS/Linux dev environments while preserving Windows compatibility
        _options.WatchFolder = NormalizePath(_options.WatchFolder);
        _options.ArchiveFolder = NormalizePath(_options.ArchiveFolder);
        _options.PrinterOutputFile = NormalizePath(_options.PrinterOutputFile);

        // ── Ensure folders exist ───────────────────────────────────────────────
        EnsureDirectory(_options.WatchFolder);
        if (_options.ArchiveProcessed) EnsureDirectory(_options.ArchiveFolder);

        // ── PHASE 1 watcher: new *.pdf files dropped into WatchFolder ─────────
        _folderWatcher = new FileSystemWatcher(_options.WatchFolder, "*.pdf")
        {
            NotifyFilter          = NotifyFilters.FileName,
            EnableRaisingEvents   = true,
            IncludeSubdirectories = false
        };
        _folderWatcher.Created += OnNewPdfDropped;
        _folderWatcher.Error   += OnWatcherError;

        // ── PHASE 2 watcher: the fixed printer output file being overwritten ───
        // The virtual printer always writes to PrinterOutputFile (same file each time).
        // We watch the FOLDER containing that file for Changed events on that filename.
        var printerFileDir  = Path.GetDirectoryName(_options.PrinterOutputFile) ?? _options.WatchFolder;
        if (string.IsNullOrWhiteSpace(printerFileDir))
        {
            printerFileDir = _options.WatchFolder;
        }
        var printerFileName = Path.GetFileName(_options.PrinterOutputFile);

        EnsureDirectory(printerFileDir);

        _printerFileWatcher = new FileSystemWatcher(printerFileDir, printerFileName)
        {
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents   = true,
            IncludeSubdirectories = false
        };
        _printerFileWatcher.Changed += OnPrinterFileChanged;
        _printerFileWatcher.Error   += OnWatcherError;

        _logger.LogInformation(
            "PDF Bridge started. Watching folder '{Folder}' for new PDFs. " +
            "Also watching printer output file '{PrinterFile}' for virtual printer jobs.",
            _options.WatchFolder, _options.PrinterOutputFile);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeWatcher(ref _folderWatcher);
        DisposeWatcher(ref _printerFileWatcher);
        _logger.LogInformation("PDF Bridge stopped.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Manually trigger a scan of all existing PDFs in the watch folder.
    /// Called by POST /api/pdf-bridge/scan from the UI.
    /// </summary>
    public void ScanNow()
    {
        _logger.LogInformation("PDF Bridge: manual scan triggered.");
        var files = Directory.GetFiles(_options.WatchFolder, "*.pdf");
        _logger.LogInformation("PDF Bridge: found {Count} PDF(s) in watch folder.", files.Length);
        foreach (var file in files)
        {
            // Skip the printer output file here — it is handled by the printer file watcher
            if (string.Equals(file, _options.PrinterOutputFile, StringComparison.OrdinalIgnoreCase))
                continue;
            ProcessPdf(file, archive: _options.ArchiveProcessed);
        }
    }

    // ── Phase 1: new file dropped into folder ─────────────────────────────────

    private void OnNewPdfDropped(object sender, FileSystemEventArgs e)
    {
        // Skip the fixed printer output file — it is handled by the printer file watcher
        if (string.Equals(e.FullPath, _options.PrinterOutputFile, StringComparison.OrdinalIgnoreCase))
            return;

        _logger.LogInformation("PDF Bridge: new file detected — '{File}'", e.Name);
        Thread.Sleep(_options.FileReadDelayMs);
        ProcessPdf(e.FullPath, archive: _options.ArchiveProcessed);
    }

    // ── Phase 2: virtual printer overwrites fixed output file ─────────────────

    private void OnPrinterFileChanged(object sender, FileSystemEventArgs e)
    {
        // Windows can fire multiple Changed events for a single write.
        // Use SHA-256 to skip duplicates: only process when content actually changed.
        Thread.Sleep(_options.FileReadDelayMs);

        if (!File.Exists(e.FullPath)) return;

        var newHash = ComputeFileHash(e.FullPath);
        if (newHash is null) return;

        lock (_hashLock)
        {
            if (newHash == _lastPrinterFileHash)
            {
                _logger.LogDebug("PDF Bridge: printer file changed but content is identical — skipping.");
                return;
            }
            _lastPrinterFileHash = newHash;
        }

        _logger.LogInformation(
            "PDF Bridge: virtual printer job detected (file changed) — '{File}'", e.Name);

        // Copy to a timestamped archive copy and process that copy
        // (so the watcher file stays unlocked for the next print job)
        var archiveCopy = ArchivePrinterOutput(e.FullPath);
        if (archiveCopy is null) return;

        ProcessPdf(archiveCopy, archive: false); // already archived above
    }

    // ── Core processing ───────────────────────────────────────────────────────

    private void ProcessPdf(string fullPath, bool archive)
    {
        string? recipientTempPath = null;
        string? supplierTempPath = null;
        bool isSplit = false;

        try
        {
            int pageCount = 0;
            try
            {
                using (var documentReader = PdfSharp.Pdf.IO.PdfReader.Open(fullPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                {
                    pageCount = documentReader.PageCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF Bridge: Failed to open PDF for page count analysis.");
            }

            // Extract data from the original printed document
            var pmsEvent = _extractor.ExtractFromPdf(fullPath, _options.DefaultEventType);

            if (pmsEvent is null)
            {
                FilesFailedExtraction++;
                LastError = $"Could not extract phone number from '{Path.GetFileName(fullPath)}'";
                _logger.LogWarning(
                    "PDF Bridge: skipping '{File}' — no phone number found. " +
                    "Ensure the guest's mobile number is printed on the bill.",
                    Path.GetFileName(fullPath));
                return;
            }

            FilesProcessed++;
            LastProcessedAt = DateTimeOffset.Now;
            LastError       = null;

            var baseFileName = BuildGuestFileName(pmsEvent);
            var baseNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
            var dir = Path.GetDirectoryName(fullPath) ?? _options.ArchiveFolder;

            string targetRecipientPath;
            string? targetSupplierPath = null;

            if (pageCount == 2)
            {
                _logger.LogInformation("PDF Bridge: 2-page PDF detected. Splitting into Recipient and Supplier slips.");

                recipientTempPath = Path.Combine(dir, $"recipient_temp_{Guid.NewGuid()}.pdf");
                supplierTempPath = Path.Combine(dir, $"supplier_temp_{Guid.NewGuid()}.pdf");

                using (var sourceDoc = PdfSharp.Pdf.IO.PdfReader.Open(fullPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                {
                    using (var recDoc = new PdfSharp.Pdf.PdfDocument())
                    {
                        recDoc.AddPage(sourceDoc.Pages[0]);
                        recDoc.Save(recipientTempPath);
                    }
                    using (var supDoc = new PdfSharp.Pdf.PdfDocument())
                    {
                        supDoc.AddPage(sourceDoc.Pages[1]);
                        supDoc.Save(supplierTempPath);
                    }
                }

                isSplit = true;

                // Apply signature to both Recipient and Supplier pages
                ApplySignatureIfAvailable(recipientTempPath);
                ApplySignatureIfAvailable(supplierTempPath);

                // Re-extract data from the signed recipient document to get the correct Base64 pdf payload
                var updatedEvent = _extractor.ExtractFromPdf(recipientTempPath, _options.DefaultEventType);
                if (updatedEvent != null)
                {
                    pmsEvent = updatedEvent;
                }

                // Load bytes of the signed recipient slip and add it to the event payload as Base64
                if (File.Exists(recipientTempPath))
                {
                    var fileBytes = File.ReadAllBytes(recipientTempPath);
                    var base64 = Convert.ToBase64String(fileBytes);
                    
                    var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(pmsEvent.RawPayload))
                    {
                        try
                        {
                            var existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(pmsEvent.RawPayload);
                            if (existing != null)
                            {
                                foreach (var kv in existing)
                                    extras[kv.Key] = kv.Value;
                            }
                        }
                        catch { }
                    }
                    extras["pdfBase64"] = base64;
                    extras["billPdfBase64"] = base64;
                    pmsEvent = pmsEvent with { RawPayload = System.Text.Json.JsonSerializer.Serialize(extras) };
                }

                targetRecipientPath = Path.Combine(dir, $"{baseNameWithoutExt}_Recipient.pdf");
                targetSupplierPath = Path.Combine(dir, $"{baseNameWithoutExt}_Supplier.pdf");
            }
            else
            {
                // For 1-page (or other size) PDFs, apply signature normally
                ApplySignatureIfAvailable(fullPath);

                // Convert PDF to Base64 to attach on WhatsApp
                if (File.Exists(fullPath))
                {
                    var fileBytes = File.ReadAllBytes(fullPath);
                    var base64 = Convert.ToBase64String(fileBytes);

                    var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(pmsEvent.RawPayload))
                    {
                        try
                        {
                            var existing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(pmsEvent.RawPayload);
                            if (existing != null)
                            {
                                foreach (var kv in existing)
                                    extras[kv.Key] = kv.Value;
                            }
                        }
                        catch { }
                    }
                    extras["pdfBase64"] = base64;
                    extras["billPdfBase64"] = base64;
                    pmsEvent = pmsEvent with { RawPayload = System.Text.Json.JsonSerializer.Serialize(extras) };
                }

                targetRecipientPath = Path.Combine(dir, $"{baseNameWithoutExt}.pdf");
            }

            _logger.LogInformation(
                "PDF Bridge: firing {EventType} for '{Guest}' ({Phone}) Room {Room}.",
                pmsEvent.EventType, pmsEvent.GuestName, pmsEvent.PhoneNumber, pmsEvent.RoomNumber);

            EventReceived?.Invoke(this, pmsEvent);

            LastProcessedFile = Path.GetFileName(targetRecipientPath);

            // Save copies to target directory
            if (isSplit && recipientTempPath != null && supplierTempPath != null && targetSupplierPath != null)
            {
                targetRecipientPath = EnsureUniqueFilePath(targetRecipientPath);
                targetSupplierPath = EnsureUniqueFilePath(targetSupplierPath);

                File.Move(recipientTempPath, targetRecipientPath, overwrite: false);
                File.Move(supplierTempPath, targetSupplierPath, overwrite: false);

                // Delete original Watch/Temp PDF
                try { File.Delete(fullPath); } catch { }
            }
            else
            {
                targetRecipientPath = EnsureUniqueFilePath(targetRecipientPath);
                if (archive)
                {
                    File.Move(fullPath, targetRecipientPath, overwrite: false);
                }
                else
                {
                    File.Move(fullPath, targetRecipientPath, overwrite: false);
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "PDF Bridge: unexpected error processing '{File}'.", fullPath);

            // Cleanup temp paths in case of error
            if (recipientTempPath != null && File.Exists(recipientTempPath)) try { File.Delete(recipientTempPath); } catch { }
            if (supplierTempPath != null && File.Exists(supplierTempPath)) try { File.Delete(supplierTempPath); } catch { }
        }
    }

    // ── File helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a human-readable PDF filename from the extracted guest data.
    /// Format: GuestName_RoomNo_InvoiceNo_YYYYMMDD.pdf
    /// Example: RameshPatel_Room101_INV1055_20260724.pdf
    /// All characters that are invalid in Windows file names are stripped.
    /// </summary>
    private static string BuildGuestFileName(Core.Models.PmsEvent pmsEvent)
    {
        var datePart = DateTimeOffset.Now.ToString("yyyyMMdd");
        var regNo = "Unknown";
        var namePart = SanitizeFileName(pmsEvent.GuestName);
        try
        {
            if (!string.IsNullOrWhiteSpace(pmsEvent.RawPayload))
            {
                var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(pmsEvent.RawPayload);
                if (payload != null)
                {
                    if (payload.TryGetValue("Registration Number", out var extractedRegNo) && !string.IsNullOrWhiteSpace(extractedRegNo))
                    {
                        regNo = SanitizeFileName(extractedRegNo);
                    }
                    else if (payload.TryGetValue("Registration No", out var extractedRegNo2) && !string.IsNullOrWhiteSpace(extractedRegNo2))
                    {
                        regNo = SanitizeFileName(extractedRegNo2);
                    }
                }
            }
        }
        catch { }

        return $"{namePart}_{regNo}_{datePart}.pdf";
    }

    private static string SanitizeFileName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var clean   = new string(input.Where(c => !invalid.Contains(c) && c != ' ').ToArray());
        return clean.Length > 50 ? clean[..50] : clean; // cap length for safety
    }

    /// <summary>
    /// Copies the printer output file to ArchiveFolder with a temporary timestamp name.
    /// The name is later updated to the guest name once extraction succeeds.
    /// Returns the temp path, or null on failure.
    /// </summary>
    private string? ArchivePrinterOutput(string sourcePath)
    {
        try
        {
            EnsureDirectory(_options.ArchiveFolder);
            // Use a temp name — ProcessPdf will rename it to the guest name after extraction
            var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            var destPath  = Path.Combine(_options.ArchiveFolder, $"bill_temp_{timestamp}.pdf");
            File.Copy(sourcePath, destPath, overwrite: true);
            _logger.LogDebug("PDF Bridge: copied printer output to temp file '{Dest}'", destPath);
            return destPath;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogError(ex, "PDF Bridge: failed to copy printer output file.");
            return null;
        }
    }

    /// <summary>
    /// Moves a PDF from the watch folder into ArchiveFolder with the guest name.
    /// Used for Phase 1 (manual drop) mode.
    /// </summary>
    private void ArchivePdf(string sourcePath, string guestFileName)
    {
        try
        {
            EnsureDirectory(_options.ArchiveFolder);
            var destPath = Path.Combine(_options.ArchiveFolder, guestFileName);
            // If a file with the same guest name already exists, add a counter suffix
            destPath = EnsureUniqueFilePath(destPath);
            File.Move(sourcePath, destPath, overwrite: false);
            _logger.LogInformation(
                "PDF Bridge: bill saved → '{Dest}'", destPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PDF Bridge: could not save bill PDF '{File}'. It will remain in the watch folder.",
                Path.GetFileName(sourcePath));
        }
    }

    /// <summary>
    /// Renames a temp archive file to the guest name.
    /// Used for Phase 2 (virtual printer) mode after extraction succeeds.
    /// </summary>
    private void RenameToGuestName(string tempPath, string guestFileName)
    {
        try
        {
            var dir      = Path.GetDirectoryName(tempPath) ?? _options.ArchiveFolder;
            var destPath = Path.Combine(dir, guestFileName);
            destPath = EnsureUniqueFilePath(destPath);
            File.Move(tempPath, destPath, overwrite: false);
            _logger.LogInformation(
                "PDF Bridge: bill saved → '{Dest}'", destPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PDF Bridge: could not rename temp file '{File}' to guest name.",
                Path.GetFileName(tempPath));
        }
    }

    /// <summary>
    /// If the target path already exists, appends (1), (2), etc. until a free name is found.
    /// Prevents overwriting an older bill for a guest with the same name.
    /// </summary>
    private static string EnsureUniqueFilePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir  = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext  = Path.GetExtension(path);
        for (int i = 1; i < 100; i++)
        {
            var candidate = Path.Combine(dir, $"{name}({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return path; // fallback: overwrite if somehow 100 duplicates exist
    }

    private static string? ComputeFileHash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hashBytes = SHA256.HashData(stream);
            return Convert.ToHexString(hashBytes);
        }
        catch
        {
            return null;
        }
    }

    private void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogInformation("PDF Bridge: created directory '{Path}'", path);
        }
    }

    private void ApplySignatureIfAvailable(string filePath)
    {
        if (GetActiveUserSignaturePath == null) return;

        try
        {
            var relativePath = GetActiveUserSignaturePath().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(relativePath)) return;

            // Resolve from the writable AppData signatures folder (set at startup).
            // Fall back to the old wwwroot path for backward compatibility.
            string sigFullPath;
            var fileName = Path.GetFileName(relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(SignaturesDataDir) && Directory.Exists(SignaturesDataDir))
            {
                sigFullPath = Path.Combine(SignaturesDataDir, fileName);
            }
            else
            {
                sigFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/'));
            }
            if (!File.Exists(sigFullPath))
            {
                _logger.LogWarning("PDF Bridge: Signature file '{Path}' not found on disk.", sigFullPath);
                return;
            }

            _logger.LogInformation("PDF Bridge: Overlaying signature '{Path}' on PDF '{Pdf}'.", sigFullPath, filePath);

            using (var document = PdfSharp.Pdf.IO.PdfReader.Open(filePath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
            {
                if (document.PageCount > 0)
                {
                    var page = document.Pages[0];
                    using (var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page))
                    {
                        using (var image = PdfSharp.Drawing.XImage.FromFile(sigFullPath))
                        {
                            double width = 85;
                            double height = 85;
                            double x = page.Width.Point - 200;
                            double y = page.Height.Point - 170;

                            gfx.DrawImage(image, x, y, width, height);
                        }
                    }
                    document.Save(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF Bridge: Failed to apply signature/stamp to PDF.");
        }
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (Path.DirectorySeparatorChar == '/')
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.Length >= 2 && normalized[1] == ':')
            {
                normalized = normalized.Substring(2);
            }
            if (normalized.StartsWith("/"))
            {
                normalized = "." + normalized;
            }
            return normalized;
        }
        return path;
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        LastError = ex.Message;
        _logger.LogError(ex, "PDF Bridge: FileSystemWatcher error. Attempting to restart watcher...");
        try
        {
            if (sender is FileSystemWatcher w)
            {
                w.EnableRaisingEvents = false;
                w.EnableRaisingEvents = true;
            }
        }
        catch (Exception restartEx)
        {
            _logger.LogError(restartEx, "PDF Bridge: could not restart FileSystemWatcher.");
        }
    }

    private static void DisposeWatcher(ref FileSystemWatcher? watcher)
    {
        if (watcher is null) return;
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        watcher = null;
    }
}
