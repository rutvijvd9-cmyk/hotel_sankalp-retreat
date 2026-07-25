using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Media.Ocr;

namespace HotelAutomation.Connectors.Pms;

using System.Text;
using System.Text.RegularExpressions;
using HotelAutomation.Core.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

/// <summary>
/// Extracts structured guest data from a printed hotel bill PDF.
///
/// Designed specifically for The Sankalp Retreat's bill format, which includes:
///   - Guest name in the "Guest Details" section
///   - Phone number embedded in the address lines (10-digit Indian mobile)
///   - Room No, Registration No, Invoice No in the top-right block
///   - Arrival/Departure dates in the top-right block
///   - CGST, SGST, Invoice Total in the summary table at the bottom
///
/// The extractor reads ALL text from all PDF pages, then uses targeted
/// regex patterns to locate each field. Patterns are ordered from most
/// to least specific to avoid false matches.
/// </summary>
public sealed class PdfDataExtractor
{
    private readonly ILogger<PdfDataExtractor> _logger;

    // ── Regex patterns matched to The Sankalp Retreat bill layout ─────────────

    // Guest name follows "Guest Details" header — grab the first non-empty line after it.
    // Handles prefixes like "MR", "MRS", "MS", "DR" etc.
    private static readonly Regex GuestNameRegex = new(
        @"Guest\s+Details?\s*[:\-]?\s*\r?\n\s*(.+?)(?:\r?\n|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Indian mobile number: 10 digits starting with 6, 7, 8, or 9.
    // Appears embedded in the address block in the guest details section.
    private static readonly Regex PhoneRegex = new(
        @"\b([6-9]\d{9})\b",
        RegexOptions.Compiled);

    // Room number — appears as "Room No : 1081" or "Room No. : 1081"
    private static readonly Regex RoomNoRegex = new(
        @"Room\s*No\.?\s*[:\-]?\s*(\d+\w*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Registration number — "Regn No : 1057"
    private static readonly Regex RegnNoRegex = new(
        @"Reg(?:n|istration)\.?\s*No\.?\s*[:\-]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Invoice number — "Invoice No : ROOM/1055"
    private static readonly Regex InvoiceNoRegex = new(
        @"Invoice\s*No\.?\s*[:\-]?\s*(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Invoice date — "Invoice Date : 22/07/2026"
    private static readonly Regex InvoiceDateRegex = new(
        @"Invoice\s*Date\s*[:\-]?\s*(\d{2}/\d{2}/\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Arrival date — "Arrival Date : 21/07/2026"
    private static readonly Regex ArrivalDateRegex = new(
        @"Arrival\s*Date\s*[:\-]?\s*(\d{2}/\d{2}/\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Departure date — "Dept. Date : 22/07/2026" or "Departure Date : 22/07/2026"
    private static readonly Regex DepartureDateRegex = new(
        @"(?:Dept\.?|Departure)\s*Date\s*[:\-]?\s*(\d{2}/\d{2}/\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Invoice Total — must match "Invoice Total : 3528.00" specifically
    // (not "Total" alone, to avoid matching intermediate sub-totals)
    private static readonly Regex InvoiceTotalRegex = new(
        @"Invoice\s+Total\s*[:\-]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // CGST amount — "CGST : 84.00"
    private static readonly Regex CgstRegex = new(
        @"CGST\s*[:\-]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // SGST amount — "SGST : 84.00"
    private static readonly Regex SgstRegex = new(
        @"SGST\s*[:\-]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Taxable value — "Taxable Value : 3360.00"
    private static readonly Regex TaxableValueRegex = new(
        @"Taxable\s+Value\s*[:\-]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Room type — "Type Of Room : Presidential Sur"
    private static readonly Regex RoomTypeRegex = new(
        @"Type\s+Of\s+Room\s*[:\-]?\s*(.+?)(?:\r?\n|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Tariff per day — "Tariff : 4500"
    private static readonly Regex TariffRegex = new(
        @"Tariff\s*[:\-]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Number of persons — "Persons : 1"
    private static readonly Regex PersonsRegex = new(
        @"Persons?\s*[:\-]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Number of days — "Days : 1"
    private static readonly Regex DaysRegex = new(
        @"\bDays?\s*[:\-]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PdfDataExtractor(ILogger<PdfDataExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Opens the PDF file, reads all text, and extracts a <see cref="PmsEvent"/>.
    /// Returns null if the phone number cannot be found (WhatsApp cannot be sent without it).
    /// </summary>
    public PmsEvent? ExtractFromPdf(string pdfPath, string defaultEventType = "CheckOut")
    {
        string fullText;
        try
        {
            fullText = ReadAllPdfText(pdfPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read PDF file: {Path}", pdfPath);
            return null;
        }

        _logger.LogDebug("PDF text extracted ({Chars} chars) from: {Path}", fullText.Length, pdfPath);

        // Fallback to OCR if no phone number was found (meaning it's likely scanned or image-only)
        var phoneMatches = PhoneRegex.Match(fullText);
        if (string.IsNullOrWhiteSpace(fullText) || !phoneMatches.Success)
        {
            _logger.LogInformation("PDF text layer is empty or missing phone number. Running OCR fallback on PDF images...");
            try
            {
                var ocrText = OcrPdfImagesAsync(pdfPath).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(ocrText))
                {
                    fullText = ocrText;
                    _logger.LogInformation("OCR fallback successful. Extracted {Chars} characters.", fullText.Length);
                }
            }
            catch (Exception ocrEx)
            {
                _logger.LogWarning(ocrEx, "OCR fallback failed for '{File}'", Path.GetFileName(pdfPath));
            }
        }

        // ── Extract all fields ─────────────────────────────────────────────────
        var guestName    = ExtractString(fullText, GuestNameRegex)?.Trim();
        var rawPhone     = ExtractString(fullText, PhoneRegex);
        var roomNumber   = ExtractString(fullText, RoomNoRegex);
        var regnNo       = ExtractString(fullText, RegnNoRegex);
        var invoiceNo    = ExtractString(fullText, InvoiceNoRegex);
        var invoiceDate  = ExtractString(fullText, InvoiceDateRegex);
        var arrivalDate  = ExtractString(fullText, ArrivalDateRegex);
        var departDate   = ExtractString(fullText, DepartureDateRegex);
        var invoiceTotal = ExtractDecimal(fullText, InvoiceTotalRegex);
        var cgst         = ExtractDecimal(fullText, CgstRegex);
        var sgst         = ExtractDecimal(fullText, SgstRegex);
        var taxableValue = ExtractDecimal(fullText, TaxableValueRegex);
        var roomType     = ExtractString(fullText, RoomTypeRegex)?.Trim();
        var tariff       = ExtractString(fullText, TariffRegex);
        var persons      = ExtractString(fullText, PersonsRegex);
        var days         = ExtractString(fullText, DaysRegex);

        // ── Phone is mandatory — cannot send WhatsApp without it ───────────────
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            _logger.LogWarning(
                "PDF Bridge: phone number not found in PDF '{File}' for guest '{Guest}'. " +
                "WhatsApp will NOT be sent. Please ensure the PMS prints the guest's mobile number on the bill.",
                Path.GetFileName(pdfPath), guestName ?? "Unknown");
            return null;
        }

        var phoneE164 = NormalizeIndianPhone(rawPhone);

        // ── Build rich payload dictionary (stored in RawPayload for template use) ──
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (regnNo       != null) extras["Registration No"]  = regnNo;
        if (invoiceNo    != null) extras["Invoice No"]       = invoiceNo;
        if (invoiceDate  != null) extras["Invoice Date"]     = invoiceDate;
        if (arrivalDate  != null) extras["Arrival Date"]     = arrivalDate;
        if (departDate   != null) extras["Departure Date"]   = departDate;
        if (cgst         != null) extras["CGST"]             = cgst.Value.ToString("0.00");
        if (sgst         != null) extras["SGST"]             = sgst.Value.ToString("0.00");
        if (taxableValue != null) extras["Taxable Value"]    = taxableValue.Value.ToString("0.00");
        if (roomType     != null) extras["Room Type"]        = roomType;
        if (tariff       != null) extras["Tariff"]           = tariff;
        if (persons      != null) extras["Persons"]          = persons;
        if (days         != null) extras["Days"]             = days;
        extras["Source PDF"] = Path.GetFileName(pdfPath);

        var rawPayload = System.Text.Json.JsonSerializer.Serialize(extras);

        _logger.LogInformation(
            "PDF Bridge: extracted data — Guest='{Guest}', Phone='{Phone}', Room='{Room}', " +
            "InvoiceNo='{InvNo}', Total={Total}, CGST={Cgst}, SGST={Sgst}",
            guestName, phoneE164, roomNumber, invoiceNo,
            invoiceTotal?.ToString("0.00") ?? "?",
            cgst?.ToString("0.00") ?? "?",
            sgst?.ToString("0.00") ?? "?");

        return new PmsEvent
        {
            EventType       = defaultEventType,
            GuestName       = guestName ?? "Guest",
            PhoneNumber     = phoneE164,
            RoomNumber      = roomNumber ?? "",
            Amount          = invoiceTotal,
            Timestamp       = DateTimeOffset.Now,
            CheckInDate     = arrivalDate,
            CheckOutDate    = departDate,
            BookingReference = invoiceNo,
            RawPayload      = rawPayload
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Uses PdfPig to extract all text from all pages, joining with newlines.
    /// PdfPig is a pure managed C# library — no native dependencies required.
    /// </summary>
    private static string ReadAllPdfText(string pdfPath)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(pdfPath);
        foreach (Page page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    private static string? ExtractString(string text, Regex regex)
    {
        var match = regex.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static decimal? ExtractDecimal(string text, Regex regex)
    {
        var raw = ExtractString(text, regex);
        if (raw is null) return null;
        // Remove commas (thousands separator) before parsing
        var cleaned = raw.Replace(",", "").Trim();
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Normalises an Indian mobile number to E.164 format (+91XXXXXXXXXX).
    /// </summary>
    private static string NormalizeIndianPhone(string raw)
    {
        raw = new string(raw.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (raw.StartsWith('+')) return raw;
        if (raw.Length == 10) return "+91" + raw;
        if (raw.Length == 11 && raw.StartsWith('0')) return "+91" + raw[1..];
        if (raw.Length >= 12) return "+" + raw;
        return raw;
    }

    private static async Task<string> OcrPdfImagesAsync(string pdfPath)
    {
        var sb = new StringBuilder();
        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (ocrEngine == null)
        {
            throw new InvalidOperationException("Windows Media OcrEngine could not be initialized.");
        }

        using var document = PdfDocument.Open(pdfPath);
        foreach (var page in document.GetPages())
        {
            foreach (var image in page.GetImages())
            {
                byte[] bytes;
                if (image.TryGetPng(out var pngBytes))
                {
                    bytes = pngBytes;
                }
                else
                {
                    image.TryGetBytes(out var rawBytesList);
                    bytes = rawBytesList?.ToArray() ?? image.RawBytes.ToArray();
                }

                if (bytes == null || bytes.Length == 0) continue;

                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(bytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                }

                var decoder = await BitmapDecoder.CreateAsync(stream);
                using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);

                var result = await ocrEngine.RecognizeAsync(softwareBitmap);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    sb.AppendLine(result.Text);
                }
            }
        }

        return sb.ToString();
    }
}
