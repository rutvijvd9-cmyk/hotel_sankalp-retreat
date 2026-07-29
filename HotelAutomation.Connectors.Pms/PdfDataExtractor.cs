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

    // Fallback for room number if not found by RoomNoRegex.
    // Matches the number at the end of the line starting with "Invoice Date".
    private static readonly Regex InvoiceDateAndRoomRegex = new(
        @"Invoice\s*Date\s*[:\-]?\s*\d{2}[\/\s\-\|lI1\\]+\d{2}[\/\s\-\|lI1\\]+\d{4}\s+(\d+\w*)",
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

    // Registration number — "Regn No : 1057" or "Regn.N0. : 1115"
    private static readonly Regex RegnNoRegex = new(
        @"Reg(?:n|istration)\.?\s*N[o0]\.?\s*[^a-zA-Z0-9]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Invoice number — "Invoice No : ROOM/1055"
    // Require alphanumeric first char to avoid catching bullet • or punctuation that
    // sometimes appears between the label and the value in OCR-rendered temp PDFs.
    private static readonly Regex InvoiceNoRegex = new(
        @"Invoice\s*No\.?\s*[^a-zA-Z0-9]?\s*([A-Za-z0-9][A-Za-z0-9/\-]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Invoice date — "Invoice Date : 22/07/2026" (handling OCR vertical bars, spaces, ones, and slashes)
    private static readonly Regex InvoiceDateRegex = new(
        @"Invoice\s*Date\s*[^a-zA-Z0-9]{0,10}\s*(\d{2})[\/\s\-\|lI1\\]+(\d{2})[\/\s\-\|lI1\\]+(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Arrival date — "Arrival Date : 21/07/2026"
    private static readonly Regex ArrivalDateRegex = new(
        @"Arrival\s*Date\s*[^a-zA-Z0-9]{0,10}\s*(\d{2})[\/\s\-\|lI1\\]+(\d{2})[\/\s\-\|lI1\\]+(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Departure date — "Dept. Date : 22/07/2026" or "Departure Date : 22/07/2026"
    // Also catches "Dep. Date", "Dept Date" (no dot), "Check Out Date" etc.
    private static readonly Regex DepartureDateRegex = new(
        @"(?:Dept\.?|Dep\.?|Departure|Check\s*Out)\s*Date\s*[^a-zA-Z0-9]{0,10}\s*(\d{2})[\/\s\-\|lI1\\]+(\d{2})[\/\s\-\|lI1\\]+(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Invoice Total — must match "Invoice Total : 3528.00" specifically
    // (not "Total" alone, to avoid matching intermediate sub-totals)
    private static readonly Regex InvoiceTotalRegex = new(
        @"Invoice\s+Total\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // CGST amount — "CGST : 84.00"
    private static readonly Regex CgstRegex = new(
        @"CGST\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // SGST amount — "SGST : 84.00"
    private static readonly Regex SgstRegex = new(
        @"SGST\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Taxable value — "Taxable Value : 3360.00"
    private static readonly Regex TaxableValueRegex = new(
        @"Taxable\s+Value\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Room type — "Type Of Room : Presidential Sur"
    private static readonly Regex RoomTypeRegex = new(
        @"Type\s+Of\s+Room\s*[^a-zA-Z0-9]?\s*(.+?)(?:\r?\n|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Tariff per day — "Tariff : 4500" or "Tariff • 5800"
    private static readonly Regex TariffRegex = new(
        @"Tariff\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Number of persons — "Persons : 1"
    private static readonly Regex PersonsRegex = new(
        @"Persons?\s*[^a-zA-Z0-9]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Number of days — "Days : 1"
    private static readonly Regex DaysRegex = new(
        @"\bDays?\s*[^a-zA-Z0-9]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Arrival time — from "Arrival Date : 27/07/2026 Time 16:05"
    // Uses [\ s\S]{0,60}? so the time can be on the SAME line OR the next OCR line,
    // which handles virtual-printer PDFs whose word-banding differs from sunil.pdf.
    private static readonly Regex ArrivalTimeRegex = new(
        @"Arrival\s*Date[\s\S]{0,60}?T[i1l]?m[e|]?\s*[^a-zA-Z0-9]?\s*(\d{2}:\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Departure time — from "Dept. Date : 28/07/2026 Time 09:40"
    // Same multi-line tolerance as ArrivalTimeRegex.
    private static readonly Regex DepartureTimeRegex = new(
        @"(?:Dept\.?|Dep\.?|Departure|Check\s*Out)\s*Date[\s\S]{0,60}?T[i1l]?m[e|]?\s*[^a-zA-Z0-9]?\s*(\d{2}:\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone fallback: any "Time HH:MM" anywhere in the text.
    // First match → Arrival Time, second match → Departure Time.
    private static readonly Regex TmeGlobalRegex = new(
        @"T[i1l]?m[e|]?\s*[^a-zA-Z0-9]?\s*(\d{2}:\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Round off — from "Round Off : 0.24" (supporting newlines)
    private static readonly Regex RoundOffRegex = new(
        @"Round\s*Off\s*(?:\r?\n)?\s*[:\-]?\s*([\d\.\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Payment mode — from "Bill To : P O S MACHINE"
    private static readonly Regex PaymentModeRegex = new(
        @"Bill\s*To\s*[:\-]?\s*\r?\n\s*(.+?)(?:\r?\n|$|Type\s+Of\s+Room)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Discount — e.g. "Discount 63.88%"
    private static readonly Regex DiscountRegex = new(
        @"Discount\s*[^a-zA-Z0-9]?\s*([\d\.]+%?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Deposit / Advance
    private static readonly Regex DepositRegex = new(
        @"(?:Deposit|Advance)\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Room Service
    private static readonly Regex RoomServiceRegex = new(
        @"Room\s*Service\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Laundry
    private static readonly Regex LaundryRegex = new(
        @"Laundr[yi]\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Misc Service
    private static readonly Regex MiscServiceRegex = new(
        @"(?:Misc|Miscellaneous)(?:\s*Service)?\s*[^a-zA-Z0-9]?\s*([\d,]+\.?\d*)",
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
        var (prefix, firstName, lastName) = ParseGuestDetails(fullText);
        var guestName = string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName}";
        if (guestName == "Guest" || string.IsNullOrWhiteSpace(guestName))
        {
            guestName = "Guest";
        }

        var rawPhone     = ExtractString(fullText, PhoneRegex);
        var roomNumber   = ExtractString(fullText, RoomNoRegex);
        if (string.IsNullOrWhiteSpace(roomNumber))
        {
            roomNumber = ExtractString(fullText, InvoiceDateAndRoomRegex);
        }
        roomNumber = NormalizeRoomNumber(roomNumber);

        var regnNo       = ExtractString(fullText, RegnNoRegex);
        regnNo = NormalizeRoomNumber(regnNo);

        var invoiceNo    = ExtractString(fullText, InvoiceNoRegex);
        if (invoiceNo != null)
        {
            if (invoiceNo.StartsWith("ROOM-", StringComparison.OrdinalIgnoreCase) || invoiceNo.StartsWith("ROOM/", StringComparison.OrdinalIgnoreCase))
            {
                var prfx = invoiceNo.Substring(0, 5);
                var rest = invoiceNo.Substring(5);
                invoiceNo = prfx + NormalizeRoomNumber(rest);
            }
        }

        var invoiceDate  = ExtractDateString(fullText, InvoiceDateRegex);
        var arrivalDate  = ExtractDateString(fullText, ArrivalDateRegex);
        var departDate   = ExtractDateString(fullText, DepartureDateRegex);

        if (string.IsNullOrWhiteSpace(departDate))
        {
            var altDeptMatch = Regex.Match(fullText, @"Date\s*[:\-]?\s*(\S+)\s*T,rne\s*[:\-]?\s*1000", RegexOptions.IgnoreCase);
            if (altDeptMatch.Success)
            {
                departDate = altDeptMatch.Groups[1].Value.Replace("W", "26");
            }
        }

        var arrivalTime  = ExtractString(fullText, ArrivalTimeRegex);
        var departTime   = ExtractString(fullText, DepartureTimeRegex);

        // Fallback: if time patterns didn't match, scan for all "Tme HH:MM" occurrences globally.
        // First occurrence = Arrival Time, second = Departure Time.
        if (string.IsNullOrWhiteSpace(arrivalTime) || string.IsNullOrWhiteSpace(departTime))
        {
            var tmeMatches = TmeGlobalRegex.Matches(fullText);
            // De-duplicate across both PDF pages (bill typically prints 2 copies)
            var distinctTimes = tmeMatches
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();
            if (string.IsNullOrWhiteSpace(arrivalTime) && distinctTimes.Count >= 1)
                arrivalTime = distinctTimes[0];
            if (string.IsNullOrWhiteSpace(departTime) && distinctTimes.Count >= 2)
                departTime = distinctTimes[1];
        }
        var roundOff     = ExtractString(fullText, RoundOffRegex);
        var paymentMode  = ExtractString(fullText, PaymentModeRegex)?.Trim();
        if (paymentMode != null)
        {
            paymentMode = Regex.Replace(paymentMode, @"\s+", " ");
        }

        var discount     = ExtractString(fullText, DiscountRegex);
        var deposit      = ExtractString(fullText, DepositRegex);
        var roomService  = ExtractString(fullText, RoomServiceRegex);
        var laundry      = ExtractString(fullText, LaundryRegex);
        var miscService  = ExtractString(fullText, MiscServiceRegex);

        var invoiceTotal = ExtractDecimal(fullText, InvoiceTotalRegex);
        var cgst         = ExtractDecimal(fullText, CgstRegex);
        var sgst         = ExtractDecimal(fullText, SgstRegex);
        var taxableValue = ExtractDecimal(fullText, TaxableValueRegex);
        var roomType     = ExtractString(fullText, RoomTypeRegex)?.Trim();
        var tariff       = ExtractString(fullText, TariffRegex);
        var persons      = ExtractString(fullText, PersonsRegex);
        var days         = ExtractString(fullText, DaysRegex);

        // Calculate days if missing
        if (string.IsNullOrWhiteSpace(days) && !string.IsNullOrWhiteSpace(arrivalDate) && !string.IsNullOrWhiteSpace(departDate))
        {
            try
            {
                var provider = System.Globalization.CultureInfo.InvariantCulture;
                if (DateTime.TryParseExact(arrivalDate, "dd/MM/yyyy", provider, System.Globalization.DateTimeStyles.None, out var arrD) &&
                    DateTime.TryParseExact(departDate, "dd/MM/yyyy", provider, System.Globalization.DateTimeStyles.None, out var depD))
                {
                    var diff = (depD - arrD).TotalDays;
                    if (diff > 0)
                    {
                        days = ((int)diff).ToString();
                    }
                }
            }
            catch { }
        }

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

        // Limit address extraction to the guest details block to avoid false positives with the hotel's details
        var guestAddressBlock = GetGuestAddressBlock(fullText);
        var (city, state, nationality) = ParseAddress(guestAddressBlock);
        if (state == "Unknown" || city == "Unknown")
        {
            var (c, s, n) = ParseAddress(fullText);
            if (state == "Unknown") state = s;
            if (city == "Unknown") city = c;
        }

        // Determine Company Name if mapped to payment mode (and it doesn't look like cash/pos/upi)
        string companyName = "-";
        if (!string.IsNullOrWhiteSpace(paymentMode) &&
            !paymentMode.Contains("CASH", StringComparison.OrdinalIgnoreCase) &&
            !paymentMode.Contains("P O S", StringComparison.OrdinalIgnoreCase) &&
            !paymentMode.Contains("POS", StringComparison.OrdinalIgnoreCase) &&
            !paymentMode.Contains("CARD", StringComparison.OrdinalIgnoreCase) &&
            !paymentMode.Contains("UPI", StringComparison.OrdinalIgnoreCase) &&
            !paymentMode.Contains("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            companyName = paymentMode;
        }

        // ── Build rich payload dictionary (stored in RawPayload for template use) ──
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        extras["Prefix"]              = prefix ?? "-";
        extras["Registration Number"] = regnNo ?? "-";
        extras["Invoice Number"]      = invoiceNo ?? "-";
        extras["Bill No"]             = "-";
        extras["Invoice Date"]        = invoiceDate ?? "-";
        extras["Arrival Date"]        = arrivalDate ?? "-";
        extras["Departure date"]      = departDate ?? "-";
        extras["Arrival Time"]        = arrivalTime ?? "-";
        extras["Departure Time"]      = departTime ?? "-";
        extras["City Name"]           = city ?? "-";
        extras["State Name"]          = state ?? "-";
        extras["Nationality"]         = nationality ?? "Indian";
        extras["Room No"]             = roomNumber ?? "-";

        extras["CGST"]             = cgst?.ToString("0.00") ?? "0.00";
        extras["SGST"]             = sgst?.ToString("0.00") ?? "0.00";
        extras["Taxable Value"]    = taxableValue?.ToString("0.00") ?? "0.00";
        extras["Room Type"]        = roomType ?? "-";
        extras["Tariff"]           = tariff ?? "-";
        extras["Room Tariff"]      = tariff ?? "-";
        extras["Final Tariff"]     = tariff ?? "-";
        extras["Persons"]          = persons ?? "-";
        extras["Days"]             = days ?? "-";
        extras["Expected Stay days"] = days ?? "-";
        extras["Expected Departure Date"] = departDate ?? "-";
        extras["days"]             = days ?? "-";
        extras["Round off"]        = roundOff ?? "0.00";
        extras["Mode of Payment/Bill To"] = paymentMode ?? "-";
        
        // Category 3 defaulted/parsed fields
        extras["Discount"]         = discount ?? "0.00";
        extras["Deposit"]          = deposit ?? "0.00";
        extras["Deposit(-)"]       = deposit ?? "0.00";
        extras["Roon Service"]     = roomService ?? "0.00"; // Typo in db column schema matched
        extras["Laundary"]         = laundry ?? "0.00";     // Typo in db column schema matched
        extras["Misc Service"]     = miscService ?? "0.00";
        extras["Company Name"]     = companyName;
        extras["AP"]               = "0.00";
        extras["MAP"]              = "0.00";
        extras["EP"]               = "0.00";
        extras["CP"]               = "0.00";
        extras["Carry Date"]       = "-";
        extras["Carry registraction No"] = "-";
        extras["Carry Amount"]     = "0.00";
        extras["C/F. Amount"]      = "0.00";
        extras["Group"]            = "-";
        extras["Person"]           = "-";
        extras["Extra Person"]     = "-";
        extras["Reservation Remark"] = "-";
        extras["Room Change"]      = "-";
        extras["Other Remark"]     = "-";
        extras["B/F Registration No"] = "-";
        extras["C/F Registration No"] = "-";

        if (invoiceTotal != null)
        {
            extras["Room Total"]     = invoiceTotal.Value.ToString("0.00");
            extras["Total"]          = invoiceTotal.Value.ToString("0.00");
            extras["Final Amount"]   = invoiceTotal.Value.ToString("0.00");
        }
        else
        {
            extras["Room Total"]     = "0.00";
            extras["Total"]          = "0.00";
            extras["Final Amount"]   = "0.00";
        }
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

    private static string? ExtractDateString(string text, Regex regex)
    {
        var match = regex.Match(text);
        if (match.Success)
        {
            if (match.Groups.Count >= 4)
            {
                return $"{match.Groups[1].Value}/{match.Groups[2].Value}/{match.Groups[3].Value}";
            }
            return match.Groups[1].Value;
        }
        return null;
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

    public static async Task<string> OcrPdfImagesAsync(string pdfPath)
    {
        var sb = new StringBuilder();
        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (ocrEngine == null)
        {
            throw new InvalidOperationException("Windows Media OcrEngine could not be initialized.");
        }

        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(pdfPath);
        var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);

        for (uint i = 0; i < pdfDoc.PageCount; i++)
        {
            using var page = pdfDoc.GetPage(i);
            using var stream = new InMemoryRandomAccessStream();
            
            // Render at 3x resolution (approx 216 DPI) for significantly higher OCR accuracy on small text
            var options = new Windows.Data.Pdf.PdfPageRenderOptions
            {
                DestinationWidth = (uint)(page.Size.Width * 3),
                DestinationHeight = (uint)(page.Size.Height * 3)
            };
            await page.RenderToStreamAsync(stream, options);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var result = await ocrEngine.RecognizeAsync(softwareBitmap);
            if (result != null && result.Lines != null)
            {
                var words = new List<OcrWord>();
                foreach (var line in result.Lines)
                {
                    if (line.Words == null) continue;
                    foreach (var word in line.Words)
                    {
                        words.Add(word);
                    }
                }

                var sortedWords = words.OrderBy(w => w.BoundingRect.Y).ToList();
                var rows = new List<List<OcrWord>>();
                foreach (var word in sortedWords)
                {
                    var row = rows.FirstOrDefault(r => 
                    {
                        var rowY = r[0].BoundingRect.Y;
                        var rowH = r[0].BoundingRect.Height;
                        return Math.Abs(word.BoundingRect.Y - rowY) < rowH * 0.6;
                    });

                    if (row != null)
                    {
                        row.Add(word);
                    }
                    else
                    {
                        rows.Add(new List<OcrWord> { word });
                    }
                }

                var sortedRows = rows
                    .OrderBy(r => r[0].BoundingRect.Y)
                    .Select(r => r.OrderBy(w => w.BoundingRect.X).ToList())
                    .ToList();

                foreach (var row in sortedRows)
                {
                    var lineText = string.Join(" ", row.Select(w => w.Text));
                    sb.AppendLine(lineText);
                }
            }
        }
        return sb.ToString();
    }

    private static (string Prefix, string FirstName, string LastName) ParseGuestDetails(string text)
    {
        int idx = text.IndexOf("Guest Details", StringComparison.OrdinalIgnoreCase);
        if (idx == -1) return ("", "Guest", "");

        var lines = text.Substring(idx).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string? nameLine = null;

        foreach (var line in lines)
        {
            // Clean common invoice properties from the line
            var cleaned = Regex.Replace(line, @"(?:Guest\s+Details|Invoice\s*No|Room\s*No|Invoice\s*Date|Regn\.?N[o0]|Arrival|Dept\s*Date|Tme|Days|•|:|-).*", "", RegexOptions.IgnoreCase).Trim();
            
            // If it's a valid alphabetic name line
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 3 && Regex.IsMatch(cleaned, @"^[a-zA-Z\s\.]+$"))
            {
                // We want to skip generic payment names if they appear here
                if (cleaned.Contains("P O S MACHINE", StringComparison.OrdinalIgnoreCase) || cleaned.Contains("CASH", StringComparison.OrdinalIgnoreCase))
                    continue;

                nameLine = cleaned;
                break;
            }
        }

        if (nameLine == null) return ("", "Guest", "");

        // Split prefix and names
        var match = Regex.Match(nameLine, @"^(MR|MRS|MS|DR|MISS)\.?\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string prefix = match.Groups[1].Value.ToUpper();
            string fullName = match.Groups[2].Value.Trim();
            var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string firstName = parts.Length > 0 ? parts[0] : "";
            string lastName = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";
            return (prefix, firstName, lastName);
        }
        else
        {
            var parts = nameLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string firstName = parts.Length > 0 ? parts[0] : "";
            string lastName = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";
            return ("", firstName, lastName);
        }
    }

    private static string GetGuestAddressBlock(string fullText)
    {
        int idx = fullText.IndexOf("Guest Details", StringComparison.OrdinalIgnoreCase);
        if (idx == -1) return fullText;

        // Take lines starting from Guest Details, stopping at "Bill To" or the Arrival/Dept section
        var lines = fullText.Substring(idx).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var blockLines = new List<string>();
        foreach (var line in lines.Take(12))
        {
            // Stop before company/payment section starts
            if (Regex.IsMatch(line, @"^\s*(?:Bill\s*To|Remarks|GSTIN\s*:|Place\s*o[ft]\s*Supply)", RegexOptions.IgnoreCase))
                break;

            // Remove common invoice fields that appear on the right-hand side
            var cleaned = Regex.Replace(line, @"(?:Invoice\s*No|Room\s*No\.?|Invoice\s*Date|Regn\.?N[o0]|Arrival\s*Date|Dept\s*Date|T[im]?me|Days).*$", "", RegexOptions.IgnoreCase).Trim();
            if (!string.IsNullOrWhiteSpace(cleaned))
                blockLines.Add(cleaned);
        }
        return string.Join("\n", blockLines);
    }

    // Full list of Indian states (and common OCR variants)
    private static readonly (string Pattern, string Canonical)[] _indianStates = new[]
    {
        ("ANDAMAN",           "ANDAMAN AND NICOBAR ISLANDS"),
        ("ANDHRA",            "ANDHRA PRADESH"),
        ("ARUNACHAL",         "ARUNACHAL PRADESH"),
        ("ASSAM",             "ASSAM"),
        ("BIHAR",             "BIHAR"),
        ("CHANDIGARH",        "CHANDIGARH"),
        ("CHHATTISGARH",      "CHHATTISGARH"),
        ("DADRA",             "DADRA AND NAGAR HAVELI"),
        ("DAMAN",             "DAMAN AND DIU"),
        ("DELHI",             "DELHI"),
        ("GOA",               "GOA"),
        ("HIMACHAL",          "HIMACHAL PRADESH"),
        ("JAMMU",             "JAMMU AND KASHMIR"),
        ("JHARKHAND",         "JHARKHAND"),
        ("KARNATAKA",         "KARNATAKA"),
        ("KERALA",            "KERALA"),
        ("LADAKH",            "LADAKH"),
        ("LAKSHADWEEP",       "LAKSHADWEEP"),
        ("MADHYA PRADESH",    "MADHYA PRADESH"),
        ("MAHARASHTRA",       "MAHARASHTRA"),
        ("MANIPUR",           "MANIPUR"),
        ("MEGHALAYA",         "MEGHALAYA"),
        ("MIZORAM",           "MIZORAM"),
        ("NAGALAND",          "NAGALAND"),
        ("ODISHA",            "ODISHA"),
        ("ORISSA",            "ODISHA"),
        ("PUDUCHERRY",        "PUDUCHERRY"),
        ("PUNJAB",            "PUNJAB"),
        ("RAJASTHAN",         "RAJASTHAN"),
        ("RAJSTHAN",          "RAJASTHAN"),
        ("SIKKIM",            "SIKKIM"),
        ("TAMIL NADU",        "TAMIL NADU"),
        ("TELANGANA",         "TELANGANA"),
        ("TRIPURA",           "TRIPURA"),
        ("UTTAR PRADESH",     "UTTAR PRADESH"),
        ("UTTARAKHAND",       "UTTARAKHAND"),
        ("WEST BENGAL",       "WEST BENGAL"),
        // GUJARAT last so it doesn't match "Place of Supply: 24-GUJARAT" before the guest address
        ("GUJARAT",           "GUJARAT"),
    };

    private static readonly Dictionary<string, string> _cityAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AHEMDABAD"]  = "Ahmedabad",
        ["AHMEDABAD"]  = "Ahmedabad",
        ["AHMDABAD"]   = "Ahmedabad",
        ["DHARMASHALA"]= "Dharamshala",
        ["DHARAMSHALA"]= "Dharamshala",
        ["RNKOT"]      = "Rajkot",
        ["RAJKOT"]     = "Rajkot",
        ["KHATRA"]     = "Khatra",
        ["DEOLI"]      = "Deoli",
        ["WARDHA"]     = "Wardha",
        ["WARSHA"]     = "Wardha",   // OCR variant
    };

    private static string NormaliseCity(string raw)
    {
        if (_cityAliases.TryGetValue(raw.Trim(), out var canonical)) return canonical;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(raw.Trim().ToLower());
    }

    private static (string City, string State, string Nationality) ParseAddress(string text)
    {
        string state = "Unknown";
        string city = "Unknown";
        string nationality = "Indian";

        // Find the first state keyword that appears in the address block text
        foreach (var (pattern, canonical) in _indianStates)
        {
            var stateIdx = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (stateIdx == -1) continue;

            state = canonical;

            // ── Per-state city extraction ────────────────────────────────────────
            if (state == "HIMACHAL PRADESH")
            {
                // Dharamshala/Dharmashala is the only city we typically see
                foreach (var c in new[] { "DHARAMSHALA", "DHARMASHALA" })
                {
                    if (text.Contains(c, StringComparison.OrdinalIgnoreCase))
                    { city = "Dharamshala"; break; }
                }
            }
            else if (state == "MAHARASHTRA")
            {
                // Address format: "AT ISAPUR PO NANDORA TAH DEOLI" → city after TAH
                var tahMatch = Regex.Match(text, @"\bTAH\s+([A-Z][A-Z]+)", RegexOptions.IgnoreCase);
                if (tahMatch.Success)
                {
                    city = NormaliseCity(tahMatch.Groups[1].Value);
                }
                else
                {
                    // Fallback: token before MAHARASHTRA on same line
                    city = ExtractCityBeforeKeyword(text, pattern);
                }
            }
            else
            {
                // General rule: token immediately before the state keyword on the same line
                city = ExtractCityBeforeKeyword(text, pattern);
            }

            break;
        }

        return (city, state, nationality);
    }

    /// <summary>Finds the last non-trivial word on the same line as <paramref name="keyword"/>, before it.</summary>
    private static string ExtractCityBeforeKeyword(string text, string keyword)
    {
        var stateIdx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (stateIdx == -1) return "Unknown";

        var lineStart = text.LastIndexOf('\n', stateIdx);
        var lineText = (lineStart == -1 ? text : text.Substring(lineStart + 1)).Trim();

        // Remove state keyword and everything after it
        var kwIdx = lineText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (kwIdx <= 0) return "Unknown";
        var beforeState = lineText.Substring(0, kwIdx).Trim();

        // Split and take last meaningful token (≥3 chars, not a number or noise)
        var tokens = beforeState
            .Split(new[] { ',', ' ', '\t', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !Regex.IsMatch(t, @"^\d+$"))
            .ToArray();

        if (tokens.Length == 0) return "Unknown";
        return NormaliseCity(tokens[^1]);
    }


    private static string NormalizeRoomNumber(string? roomVal)
    {
        if (string.IsNullOrWhiteSpace(roomVal)) return "";
        roomVal = Regex.Replace(roomVal, @"^ROOM[-/]?\s*", "", RegexOptions.IgnoreCase);
        var clean = roomVal.Replace("i", "1")
                           .Replace("I", "1")
                           .Replace("l", "1")
                           .Replace("|", "1")
                           .Replace("o", "0")
                           .Replace("O", "0")
                           .Replace("-", "")
                           .Replace(" ", "");
        if (clean.Length == 5 && clean.StartsWith("1110"))
        {
            clean = "1" + clean.Substring(2);
        }
        return clean;
    }
}
