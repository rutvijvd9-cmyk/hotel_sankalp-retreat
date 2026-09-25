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
            NotifyFilter          = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents   = true,
            IncludeSubdirectories = false
        };
        _printerFileWatcher.Changed += OnPrinterFileChanged;
        _printerFileWatcher.Created += OnPrinterFileChanged;
        _printerFileWatcher.Renamed += (s, e) => OnPrinterFileChanged(s, new FileSystemEventArgs(WatcherChangeTypes.Changed, printerFileDir, e.Name));
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

        // Check if the printer output file has unprocessed content
        if (File.Exists(_options.PrinterOutputFile))
        {
            try
            {
                var currentHash = ComputeFileHash(_options.PrinterOutputFile);
                if (currentHash != null && currentHash != _lastPrinterFileHash)
                {
                    _logger.LogInformation("PDF Bridge: found unprocessed printer output file during scan.");
                    lock (_hashLock)
                    {
                        _lastPrinterFileHash = currentHash;
                    }
                    var archiveCopy = ArchivePrinterOutput(_options.PrinterOutputFile);
                    if (archiveCopy != null)
                    {
                        ProcessPdf(archiveCopy, archive: false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PDF Bridge: error checking printer output file during ScanNow.");
            }
        }

        var files = Directory.GetFiles(_options.WatchFolder, "*.pdf");
        _logger.LogInformation("PDF Bridge: found {Count} PDF(s) in watch folder.", files.Length);
        foreach (var file in files)
        {
            // Skip the printer output file here — it was handled above
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

            // Check if Departure Date matches Current Date -> activate CheckOut automation
            if (!string.IsNullOrWhiteSpace(pmsEvent.CheckOutDate))
            {
                try
                {
                    var todayStr = DateTime.Now.ToString("dd/MM/yyyy");
                    if (string.Equals(pmsEvent.CheckOutDate.Trim(), todayStr, StringComparison.OrdinalIgnoreCase))
                    {
                        pmsEvent = pmsEvent with { EventType = "CheckOut" };
                        _logger.LogInformation("PDF Bridge: Departure date '{DeptDate}' matches current date '{Today}' — CheckOut automation activated.", pmsEvent.CheckOutDate, todayStr);
                    }
                }
                catch { }
            }

            FilesProcessed++;
            LastProcessedAt = DateTimeOffset.Now;
            LastError       = null;

            var baseFileName = BuildGuestFileName(pmsEvent);
            var baseNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);

            var dateFolder = GetDateFolderName(pmsEvent);
            var dateDir = Path.Combine(_options.ArchiveFolder, dateFolder);
            var noSigDir = Path.Combine(dateDir, "No signature and stamp");
            var sigDir = Path.Combine(dateDir, "Signature and stamp");

            EnsureDirectory(noSigDir);
            EnsureDirectory(sigDir);

            string targetRecipientPath;
            string? targetSupplierPath = null;

            if (pageCount == 2)
            {
                _logger.LogInformation("PDF Bridge: 2-page PDF detected. Splitting into Recipient and Supplier slips.");

                // Save un-signed split pages to "No signature and stamp" (normalized & centered)
                var noSigRecipientPath = EnsureUniqueFilePath(Path.Combine(noSigDir, $"{baseNameWithoutExt}_Recipient.pdf"));
                var noSigSupplierPath = EnsureUniqueFilePath(Path.Combine(noSigDir, $"{baseNameWithoutExt}_Supplier.pdf"));

                using (var sourceDoc = PdfSharp.Pdf.IO.PdfReader.Open(fullPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                {
                    using (var recDoc = new PdfSharp.Pdf.PdfDocument())
                    {
                        var p0 = recDoc.AddPage(sourceDoc.Pages[0]);
                        CopyPageBoxes(sourceDoc.Pages[0], p0);
                        recDoc.Save(noSigRecipientPath);
                    }
                    using (var supDoc = new PdfSharp.Pdf.PdfDocument())
                    {
                        var p1 = supDoc.AddPage(sourceDoc.Pages[1]);
                        CopyPageBoxes(sourceDoc.Pages[1], p1);
                        supDoc.Save(noSigSupplierPath);
                    }
                }

                NormalizeAndScalePdfFile(noSigRecipientPath);
                NormalizeAndScalePdfFile(noSigSupplierPath);

                recipientTempPath = Path.Combine(_options.ArchiveFolder, $"recipient_temp_{Guid.NewGuid()}.pdf");
                supplierTempPath = Path.Combine(_options.ArchiveFolder, $"supplier_temp_{Guid.NewGuid()}.pdf");

                using (var sourceDoc = PdfSharp.Pdf.IO.PdfReader.Open(fullPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
                {
                    using (var recDoc = new PdfSharp.Pdf.PdfDocument())
                    {
                        var p0 = recDoc.AddPage(sourceDoc.Pages[0]);
                        CopyPageBoxes(sourceDoc.Pages[0], p0);
                        recDoc.Save(recipientTempPath);
                    }
                    using (var supDoc = new PdfSharp.Pdf.PdfDocument())
                    {
                        var p1 = supDoc.AddPage(sourceDoc.Pages[1]);
                        CopyPageBoxes(sourceDoc.Pages[1], p1);
                        supDoc.Save(supplierTempPath);
                    }
                }

                isSplit = true;

                // Scale and align pages first, then stamp signature
                NormalizeAndScalePdfFile(recipientTempPath);
                NormalizeAndScalePdfFile(supplierTempPath);

                ApplySignatureIfAvailable(recipientTempPath);
                ApplySignatureIfAvailable(supplierTempPath);

                // Re-extract data from the signed recipient document
                var updatedEvent = _extractor.ExtractFromPdf(recipientTempPath, _options.DefaultEventType);
                if (updatedEvent != null)
                {
                    pmsEvent = updatedEvent;
                }

                targetRecipientPath = Path.Combine(sigDir, $"{baseNameWithoutExt}_Recipient.pdf");
                targetSupplierPath = Path.Combine(sigDir, $"{baseNameWithoutExt}_Supplier.pdf");

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

                extras.Remove("pdfBase64");
                extras.Remove("billPdfBase64");

                extras["Recipient No Signature & Stamp"] = Path.Combine("PDFs", "Processed", dateFolder, "No signature and stamp", Path.GetFileName(noSigRecipientPath)).Replace('\\', '/');
                extras["Supplier No Signature & Stamp"] = Path.Combine("PDFs", "Processed", dateFolder, "No signature and stamp", Path.GetFileName(noSigSupplierPath)).Replace('\\', '/');
                extras["Recipient Signature & Stamp"] = Path.Combine("PDFs", "Processed", dateFolder, "Signature and stamp", Path.GetFileName(targetRecipientPath)).Replace('\\', '/');
                extras["Supplier Signature & Stamp"] = Path.Combine("PDFs", "Processed", dateFolder, "Signature and stamp", Path.GetFileName(targetSupplierPath)).Replace('\\', '/');

                pmsEvent = pmsEvent with { RawPayload = System.Text.Json.JsonSerializer.Serialize(extras) };
            }
            else
            {
                // Save un-signed original file to "No signature and stamp"
                var noSigPath = EnsureUniqueFilePath(Path.Combine(noSigDir, $"{baseNameWithoutExt}.pdf"));
                File.Copy(fullPath, noSigPath, overwrite: true);
                NormalizeAndScalePdfFile(noSigPath);

                // For 1-page (or other size) PDFs, scale & center first then apply signature
                NormalizeAndScalePdfFile(fullPath);
                ApplySignatureIfAvailable(fullPath);

                targetRecipientPath = Path.Combine(sigDir, $"{baseNameWithoutExt}.pdf");

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

                extras.Remove("pdfBase64");
                extras.Remove("billPdfBase64");

                extras["Recipient No Signature & Stamp"] = Path.Combine("PDFs", "Processed", dateFolder, "No signature and stamp", Path.GetFileName(noSigPath)).Replace('\\', '/');
                extras["Recipient Signature & Stamp"] = Path.Combine("PDFs", "Processed", dateFolder, "Signature and stamp", Path.GetFileName(targetRecipientPath)).Replace('\\', '/');

                pmsEvent = pmsEvent with { RawPayload = System.Text.Json.JsonSerializer.Serialize(extras) };
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
                if (File.Exists(targetRecipientPath))
                {
                    File.Delete(targetRecipientPath);
                }
                File.Move(fullPath, targetRecipientPath, overwrite: false);
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
    /// Extracts the invoice date (or arrival/departure date) from extracted PmsEvent data,
    /// formatted cleanly for Windows folder names (e.g. "07-08-2026").
    /// </summary>
    private static string GetDateFolderName(Core.Models.PmsEvent pmsEvent)
    {
        string? rawDate = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(pmsEvent.RawPayload))
            {
                var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(pmsEvent.RawPayload);
                if (payload != null)
                {
                    if (payload.TryGetValue("Invoice Date", out var invDate) && !string.IsNullOrWhiteSpace(invDate) && invDate != "-")
                        rawDate = invDate;
                    else if (payload.TryGetValue("Arrival Date", out var arrDate) && !string.IsNullOrWhiteSpace(arrDate) && arrDate != "-")
                        rawDate = arrDate;
                    else if (payload.TryGetValue("Departure date", out var depDate) && !string.IsNullOrWhiteSpace(depDate) && depDate != "-")
                        rawDate = depDate;
                }
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(rawDate))
        {
            rawDate = !string.IsNullOrWhiteSpace(pmsEvent.CheckOutDate) ? pmsEvent.CheckOutDate : pmsEvent.CheckInDate;
        }

        if (string.IsNullOrWhiteSpace(rawDate) || rawDate == "-")
        {
            return DateTime.Now.ToString("dd-MM-yyyy");
        }

        // Convert slashes, dots, spaces, backslashes and colons to hyphens (e.g. "07/08/2026" -> "07-08-2026")
        var cleanDate = rawDate.Trim().Replace('/', '-').Replace('.', '-').Replace('\\', '-').Replace(':', '-');
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(cleanDate.Where(c => !invalid.Contains(c)).ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? DateTime.Now.ToString("dd-MM-yyyy") : sanitized;
    }

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

    /// <summary>
    /// Copies MediaBox, CropBox, TrimBox, BleedBox, ArtBox, and Rotate from the
    /// source page (resolving inherited values from the parent page tree) into the
    /// destination page's dictionary. This is needed because PdfSharp's AddPage()
    /// imports the page content but does NOT resolve inherited box attributes, so the
    /// extracted page ends up with a different size than the original.
    /// </summary>
    private static void CopyPageBoxes(PdfSharp.Pdf.PdfPage src, PdfSharp.Pdf.PdfPage dst)
    {
        try
        {
            if (src.MediaBox != null) dst.MediaBox = src.MediaBox;
            if (src.CropBox != null) dst.CropBox = src.CropBox;
            if (src.TrimBox != null) dst.TrimBox = src.TrimBox;
            dst.Orientation = src.Orientation;
        }
        catch { }

        // Helper: look up a key in the page dict, walking up /Parent chain if needed
        static PdfSharp.Pdf.PdfItem? Resolve(PdfSharp.Pdf.PdfDictionary dict, string key)
        {
            var d = dict;
            while (d != null)
            {
                if (d.Elements.ContainsKey(key))
                    return d.Elements[key];

                if (d.Elements.ContainsKey("/Parent"))
                {
                    var parentRef = d.Elements["/Parent"] as PdfSharp.Pdf.Advanced.PdfReference;
                    d = parentRef?.Value as PdfSharp.Pdf.PdfDictionary;
                }
                else break;
            }
            return null;
        }

        // PdfPage exposes its underlying PdfDictionary through the inherited base
        var srcDict = src as PdfSharp.Pdf.PdfDictionary;
        var dstDict = dst as PdfSharp.Pdf.PdfDictionary;
        if (srcDict == null || dstDict == null) return;

        // Copy the standard page boxes
        foreach (var boxKey in new[] { "/MediaBox", "/CropBox", "/TrimBox", "/BleedBox", "/ArtBox" })
        {
            var value = Resolve(srcDict, boxKey);
            if (value != null)
            {
                dstDict.Elements[boxKey] = value;
            }
        }

        // Copy /Rotate if present
        var rotate = Resolve(srcDict, "/Rotate");
        if (rotate != null)
            dstDict.Elements["/Rotate"] = rotate;
    }

    /// <summary>
    /// Scales and centers the printed PDF pages on standard A4 (595.28 x 841.89 pt) so that
    /// left and right margins are completely even, and content fills the page with balanced gaps.
    /// </summary>
    private void NormalizeAndScalePdfFile(string pdfPath)
    {
        try
        {
            var tempScaledPath = pdfPath + $".scaled_{Guid.NewGuid():N}.pdf";
            const double standardWidth = 595.27559;  // A4 width in pt
            const double standardHeight = 841.88976; // A4 height in pt

            using (var xForm = PdfSharp.Drawing.XPdfForm.FromFile(pdfPath))
            {
                if (xForm.PageCount == 0) return;

                using (var destDoc = new PdfSharp.Pdf.PdfDocument())
                {
                    for (int i = 0; i < xForm.PageCount; i++)
                    {
                        xForm.PageNumber = i + 1;
                        double originalWidth = xForm.PixelWidth > 0 ? xForm.PointWidth : standardWidth;
                        double originalHeight = xForm.PixelHeight > 0 ? xForm.PointHeight : standardHeight;

                        var newPage = destDoc.AddPage();
                        newPage.Width = PdfSharp.Drawing.XUnit.FromPoint(standardWidth);
                        newPage.Height = PdfSharp.Drawing.XUnit.FromPoint(standardHeight);

                        using (var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(newPage))
                        {
                            // Scale factor: 0.98 scale ensures content is nicely proportioned without touching borders
                            double scaleX = (standardWidth / originalWidth) * 0.98;
                            double scaleY = (standardHeight / originalHeight) * 0.98;
                            double scale = Math.Min(scaleX, scaleY);

                            double scaledWidth = originalWidth * scale;
                            double scaledHeight = originalHeight * scale;

                            // Center the scaled content on the new A4 page with equal left/right/top/bottom margins
                            double offsetX = (standardWidth - scaledWidth) / 2.0;
                            double offsetY = (standardHeight - scaledHeight) / 2.0;

                            gfx.DrawImage(xForm, offsetX, offsetY, scaledWidth, scaledHeight);
                        }
                    }

                    destDoc.Save(tempScaledPath);
                }
            }

            if (File.Exists(tempScaledPath))
            {
                File.Move(tempScaledPath, pdfPath, overwrite: true);
                _logger.LogInformation("PDF Bridge: Normalized and auto-aligned PDF pages for '{File}'.", Path.GetFileName(pdfPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF Bridge: Could not auto-scale PDF '{File}' (using original layout).", Path.GetFileName(pdfPath));
        }
    }

    private void ApplySignatureIfAvailable(string filePath)
    {
        if (GetActiveUserSignaturePath == null) return;

        try
        {
            var relativePath = GetActiveUserSignaturePath().GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                _logger.LogWarning("PDF Bridge: GetActiveUserSignaturePath returned null or empty.");
                return;
            }

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
                var wwwrootFallback = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.TrimStart('/'));
                if (File.Exists(wwwrootFallback))
                {
                    sigFullPath = wwwrootFallback;
                }
                else
                {
                    _logger.LogWarning("PDF Bridge: Signature file '{Path}' not found on disk.", sigFullPath);
                    return;
                }
            }

            _logger.LogInformation("PDF Bridge: Overlaying signature '{Path}' on PDF '{Pdf}'.", sigFullPath, filePath);

            var tempSigPath = filePath + $".sigtemp_{Guid.NewGuid()}.pdf";

            // Open in Modify mode and append the signature graphic directly to existing pages.
            // This preserves 100% of original vector layout, text scale, and page dimensions.
            using (var document = PdfSharp.Pdf.IO.PdfReader.Open(filePath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify))
            {
                for (int i = 0; i < document.PageCount; i++)
                {
                    var page = document.Pages[i];
                    using (var gfx = PdfSharp.Drawing.XGraphics.FromPdfPage(page, PdfSharp.Drawing.XGraphicsPdfPageOptions.Append))
                    {
                        using (var image = PdfSharp.Drawing.XImage.FromFile(sigFullPath))
                        {
                            double pageWidth = page.Width.Point > 0 ? page.Width.Point : 595.275;
                            double pageHeight = page.Height.Point > 0 ? page.Height.Point : 841.89;

                            // Stamp dimensions: Maintain true aspect ratio of the uploaded image
                            double targetSize = 95;
                            double imgAspect = (image.PixelHeight > 0) ? ((double)image.PixelWidth / image.PixelHeight) : 1.0;
                            double stampWidth = targetSize;
                            double stampHeight = targetSize;

                            if (imgAspect > 1.0)
                            {
                                stampWidth = targetSize;
                                stampHeight = targetSize / imgAspect;
                            }
                            else if (imgAspect > 0)
                            {
                                stampHeight = targetSize;
                                stampWidth = targetSize * imgAspect;
                            }
                            
                            // Align horizontally over the right signature area
                            double x = pageWidth - 215;
                            // Position vertically down from "For, Hotel Sankalp" so bottom part overlaps "Authorised Signature"
                            double y = pageHeight - 205;

                            gfx.DrawImage(image, x, y, stampWidth, stampHeight);
                        }
                    }
                }
                document.Save(tempSigPath);
            }

            if (File.Exists(tempSigPath))
            {
                File.Move(tempSigPath, filePath, overwrite: true);
                _logger.LogInformation("PDF Bridge: Successfully applied signature stamp to '{Pdf}'.", filePath);
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
