namespace HotelAutomation.Connectors.Pms;

/// <summary>
/// Configuration for the PDF Print Bridge connector.
/// Bind from appsettings.json section "PdfBridge".
///
/// Two modes of operation:
///
/// PHASE 1 — Manual drop:
///   Drop any PDF into WatchFolder. The connector detects it and processes it.
///
/// PHASE 2 — Virtual printer:
///   "Hotel Sankalp Printer" appears in Windows printer list.
///   Printing to it always saves to PrinterOutputFile.
///   The connector watches that file for changes and processes each new print job.
/// </summary>
public sealed class PdfBridgeOptions
{
    public const string SectionName = "PdfBridge";

    /// <summary>
    /// Folder to watch for new PDF files (Phase 1 — manual drop mode).
    /// Also used as the parent folder for the printer output file.
    /// Example: C:\HotelPrints\Output
    /// </summary>
    public string WatchFolder { get; set; } = @"C:\HotelPrints\Output";

    /// <summary>
    /// The fixed file path where the virtual Windows printer always saves PDFs.
    /// This path is used as the Windows LOCAL PORT name during printer installation.
    /// The connector watches this file for changes (Phase 2 — virtual printer mode).
    /// Example: C:\HotelPrints\Output\sankalp_bill.pdf
    /// </summary>
    public string PrinterOutputFile { get; set; } = @"C:\HotelPrints\Output\sankalp_bill.pdf";

    /// <summary>
    /// The name that appears in the Windows "Printers and Devices" list.
    /// Hotel staff select this when printing a guest bill.
    /// </summary>
    public string VirtualPrinterName { get; set; } = "Hotel Sankalp Printer";

    /// <summary>
    /// Folder to move processed PDFs into (for archiving / audit trail).
    /// Set ArchiveProcessed = false to leave PDFs in WatchFolder.
    /// </summary>
    public string ArchiveFolder { get; set; } = @"C:\HotelPrints\Processed";

    /// <summary>
    /// If true, successfully processed PDFs are moved to ArchiveFolder.
    /// If false, PDFs remain in WatchFolder.
    /// </summary>
    public bool ArchiveProcessed { get; set; } = true;

    /// <summary>
    /// The PMS event type to fire when a PDF is detected.
    /// "CheckOut" fires the checkout WhatsApp message (default).
    /// Must match an AutomationRule.TriggerEventType in the database.
    /// </summary>
    public string DefaultEventType { get; set; } = "CheckOut";

    /// <summary>
    /// Milliseconds to wait after a PDF is detected before reading it,
    /// to ensure the printer has finished writing the file.
    /// </summary>
    public int FileReadDelayMs { get; set; } = 800;
}

