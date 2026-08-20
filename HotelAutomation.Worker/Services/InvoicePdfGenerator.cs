namespace HotelAutomation.Worker.Services;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public sealed class InvoicePdfGenerator : IPdfGenerator
{
    private const string EmeraldDark = "#063D34";
    private const string EmeraldMedium = "#0D4A3E";
    private const string EmeraldLight = "#E8F3F1";
    private const string GoldAccent = "#C5A880";
    private const string NeutralDark = "#1A202C";
    private const string NeutralMuted = "#64748B";
    private const string BorderColor = "#CBD5E1";
    private const string CardBg = "#F8FAFC";

    public Task<byte[]> GenerateInvoicePdfAsync(ScheduledJob job, CancellationToken ct = default)
    {
        // ── Parse parameters with comprehensive fallbacks ──
        string guestName = !string.IsNullOrWhiteSpace(job.GuestName) ? job.GuestName : "Valued Guest";
        string roomNumber = "-";
        string roomType = "PREMIUM ROOM";
        string checkIn = "";
        string checkInTime = "12:00";
        string checkOut = "";
        string checkOutTime = "11:00";
        string amount = "0.00";
        string hotelName = "The Sankalp Retreat";
        string invoiceNo = $"ROOM/{new Random().Next(1000, 1999)}";
        string regnNo = $"{new Random().Next(1000, 1999)}";
        string invoiceDate = DateTime.Now.ToString("dd/MM/yyyy");
        string gstin = "24BUTPS6192P2ZD";
        string guestGstin = "-";
        string placeOfSupply = "24-GUJARAT";
        string guestAddress = "";
        string guestPhone = "";
        int days = 1;
        int persons = 1;

        try
        {
            if (!string.IsNullOrWhiteSpace(job.ParametersJson))
            {
                var p = JsonSerializer.Deserialize<JsonElement>(job.ParametersJson);

                if (p.TryGetProperty("Name", out var nProp)) guestName = nProp.GetString() ?? guestName;
                if (p.TryGetProperty("guest_name", out var gnProp)) guestName = gnProp.GetString() ?? guestName;

                if (p.TryGetProperty("Room", out var roomProp)) roomNumber = roomProp.GetString() ?? roomNumber;
                if (p.TryGetProperty("room_number", out var roomProp2)) roomNumber = roomProp2.GetString() ?? roomNumber;
                if (p.TryGetProperty("RoomNo", out var roomProp3)) roomNumber = roomProp3.GetString() ?? roomNumber;

                if (p.TryGetProperty("RoomType", out var rtProp)) roomType = rtProp.GetString() ?? roomType;
                if (p.TryGetProperty("room_type", out var rtProp2)) roomType = rtProp2.GetString() ?? roomType;

                if (p.TryGetProperty("CheckInDate", out var ciProp)) checkIn = ciProp.GetString() ?? "";
                if (p.TryGetProperty("check_in", out var ciProp2)) checkIn = ciProp2.GetString() ?? checkIn;
                if (p.TryGetProperty("Arrival Date", out var arrProp)) checkIn = arrProp.GetString() ?? checkIn;

                if (p.TryGetProperty("CheckInTime", out var citProp)) checkInTime = citProp.GetString() ?? checkInTime;
                if (p.TryGetProperty("ArrivalTime", out var citProp2)) checkInTime = citProp2.GetString() ?? checkInTime;

                if (p.TryGetProperty("CheckOutDate", out var coProp)) checkOut = coProp.GetString() ?? "";
                if (p.TryGetProperty("check_out", out var coProp2)) checkOut = coProp2.GetString() ?? checkOut;
                if (p.TryGetProperty("Dept Date", out var depProp)) checkOut = depProp.GetString() ?? checkOut;

                if (p.TryGetProperty("CheckOutTime", out var cotProp)) checkOutTime = cotProp.GetString() ?? checkOutTime;
                if (p.TryGetProperty("DeptTime", out var cotProp2)) checkOutTime = cotProp2.GetString() ?? checkOutTime;

                if (p.TryGetProperty("Amount", out var amtProp) && amtProp.GetString() != "0.00") amount = amtProp.GetString() ?? amount;
                if (p.TryGetProperty("total_bill", out var amtProp2)) amount = amtProp2.GetString() ?? amount;
                if (p.TryGetProperty("Total", out var amtProp3)) amount = amtProp3.GetString() ?? amount;
                if (p.TryGetProperty("Final Amount", out var amtProp4)) amount = amtProp4.GetString() ?? amount;

                if (p.TryGetProperty("Invoice Number", out var inProp)) invoiceNo = inProp.GetString() ?? invoiceNo;
                if (p.TryGetProperty("invoice_no", out var inProp2)) invoiceNo = inProp2.GetString() ?? invoiceNo;
                if (p.TryGetProperty("InvoiceNo", out var inProp3)) invoiceNo = inProp3.GetString() ?? invoiceNo;

                if (p.TryGetProperty("Invoice Date", out var idProp)) invoiceDate = idProp.GetString() ?? invoiceDate;
                if (p.TryGetProperty("Regn No", out var rnProp)) regnNo = rnProp.GetString() ?? regnNo;

                if (p.TryGetProperty("Days", out var dProp) && int.TryParse(dProp.GetString(), out var parsedDays)) days = parsedDays;
                if (p.TryGetProperty("Persons", out var perProp) && int.TryParse(perProp.GetString(), out var parsedPersons)) persons = parsedPersons;

                if (p.TryGetProperty("Address", out var addrProp)) guestAddress = addrProp.GetString() ?? guestAddress;
                if (p.TryGetProperty("Phone", out var phProp)) guestPhone = phProp.GetString() ?? guestPhone;
                if (p.TryGetProperty("GuestGSTIN", out var ggProp)) guestGstin = ggProp.GetString() ?? guestGstin;
                if (p.TryGetProperty("PlaceOfSupply", out var posProp)) placeOfSupply = posProp.GetString() ?? placeOfSupply;
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(checkIn)) checkIn = DateTime.Now.ToString("dd/MM/yyyy");
        if (string.IsNullOrWhiteSpace(checkOut)) checkOut = DateTime.Now.ToString("dd/MM/yyyy");

        decimal totalAmount = 0m;
        decimal.TryParse(amount, out totalAmount);
        if (totalAmount <= 0m) totalAmount = 4200.00m; // Default sample

        // Determine GST breakdown based on rate:
        // By standard hotel room GST: 12% if <= 7500, or 5% if configured
        decimal cgstRate = 2.50m;
        decimal sgstRate = 2.50m;
        decimal totalGstRate = cgstRate + sgstRate;

        decimal taxableValue = Math.Round(totalAmount / (1m + (totalGstRate / 100m)), 2);
        decimal cgstAmount = Math.Round(taxableValue * (cgstRate / 100m), 2);
        decimal sgstAmount = Math.Round(taxableValue * (sgstRate / 100m), 2);
        decimal tariffPerDay = Math.Round(taxableValue / Math.Max(1, days), 2);
        decimal calculatedTotal = taxableValue + cgstAmount + sgstAmount;
        decimal roundOff = totalAmount - calculatedTotal;

        string amountInWords = NumberToWords((long)Math.Round(totalAmount)) + " RUPEES ONLY";

        // Try to locate hotel logo image if available on disk
        byte[]? logoBytes = null;
        try
        {
            var logoPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "logo.png");
            if (!File.Exists(logoPath)) logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "logo.png");
            if (File.Exists(logoPath)) logoBytes = File.ReadAllBytes(logoPath);
        }
        catch { }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                // Expanded margins: 15pt horizontal, 12pt vertical for maximum usable page area
                page.MarginHorizontal(15, Unit.Point);
                page.MarginVertical(12, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica").FontColor(NeutralDark));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });

            void ComposeHeader(IContainer container)
            {
                // PaddingHorizontal(15) matches the reduced page body margin so all elements align correctly.
                container.Column(col =>
                {
                    // Luxury Emerald Banner – full bleed background, inner content padded to match body
                    col.Item().Background(EmeraldDark).PaddingHorizontal(15).PaddingVertical(12).Row(row =>
                    {
                        row.RelativeItem(7).Column(c =>
                        {
                            c.Item().Text(hotelName.ToUpperInvariant()).FontSize(17).Bold().FontColor(Colors.White).LetterSpacing(0.05f);
                            c.Item().Text("STEP INTO LUXURY").FontSize(8).FontColor(GoldAccent).LetterSpacing(0.12f);
                            c.Item().PaddingTop(4).Text("2162, A/2, Sanskarmandal, Bhavnagar - 364002, Gujarat").FontSize(7.5f).FontColor(Colors.White);
                            c.Item().Text("Phone: 0278 2225930, 0278 2572424 | Mobile: +91 9690409904").FontSize(7.5f).FontColor(Colors.White);
                            c.Item().Text("Email: thesankalpretreat@gmail.com | Web: www.thesankalpretreat.in").FontSize(7.5f).FontColor(Colors.White);
                        });

                        row.RelativeItem(3).AlignRight().AlignMiddle().Column(c =>
                        {
                            if (logoBytes != null && logoBytes.Length > 0)
                            {
                                c.Item().MaxHeight(50).Image(logoBytes);
                            }
                            else
                            {
                                c.Item().Border(1).BorderColor(GoldAccent).Padding(6).AlignCenter().Text("★ ★ ★ ★ ★").FontSize(10).FontColor(GoldAccent);
                            }
                        });
                    });

                    // Sub-Header Bar: GSTIN | TAX INVOICE | ORIGINAL FOR RECIPIENT
                    // Equal RelativeItem(1) columns so the TAX INVOICE badge is truly centred.
                    // PaddingHorizontal(15) matches the page body margin so left/right align perfectly.
                    col.Item().PaddingTop(6).PaddingHorizontal(15).Row(row =>
                    {
                        row.RelativeItem(1).AlignLeft().AlignMiddle().Text(t =>
                        {
                            t.Span("GSTIN : ").Bold().FontSize(8.5f);
                            t.Span(gstin).SemiBold().FontSize(8.5f).FontColor(EmeraldMedium);
                        });

                        row.RelativeItem(1).AlignCenter().AlignMiddle().Container()
                            .Background(EmeraldLight).PaddingHorizontal(14).PaddingVertical(3)
                            .Text("TAX INVOICE").Bold().FontSize(11).FontColor(EmeraldDark).LetterSpacing(0.08f);

                        row.RelativeItem(1).AlignRight().AlignMiddle().Text("ORIGINAL FOR RECIPIENT")
                            .FontSize(8).Italic().FontColor(NeutralMuted);
                    });

                    col.Item().PaddingTop(6).PaddingHorizontal(15).LineHorizontal(1).LineColor(BorderColor);
                });
            }

            void ComposeContent(IContainer container)
            {
                container.PaddingTop(10).Column(mainCol =>
                {
                    mainCol.Spacing(10);

                    // ── 2-Column Balanced Guest & Invoice Details Block ──
                    mainCol.Item().Row(row =>
                    {
                        // Left Column: Guest Details Card
                        row.RelativeItem().Border(1).BorderColor(BorderColor).Background(CardBg).Padding(10).Column(c =>
                        {
                            c.Spacing(3);
                            c.Item().Text("GUEST DETAILS").Bold().FontSize(9).FontColor(EmeraldDark);
                            c.Item().LineHorizontal(0.5f).LineColor(BorderColor);

                            c.Item().PaddingTop(2).Text(guestName.ToUpperInvariant()).Bold().FontSize(10).FontColor(NeutralDark);
                            if (!string.IsNullOrWhiteSpace(guestAddress))
                                c.Item().Text(guestAddress).FontSize(8.5f);
                            if (!string.IsNullOrWhiteSpace(guestPhone))
                                c.Item().Text($"Mobile: {guestPhone}").FontSize(8.5f).SemiBold();

                            c.Item().PaddingTop(4).Text(t =>
                            {
                                t.Span("Guest GSTIN: ").SemiBold();
                                t.Span(string.IsNullOrWhiteSpace(guestGstin) ? "-" : guestGstin);
                            });

                            c.Item().Text(t =>
                            {
                                t.Span("Place of Supply: ").SemiBold();
                                t.Span(placeOfSupply);
                            });

                            c.Item().Text(t =>
                            {
                                t.Span("Bill To: ").SemiBold();
                                t.Span("CASH / DIRECT");
                            });
                        });

                        row.ConstantItem(12); // Spacing between columns

                        // Right Column: Stay & Invoice Metadata Card
                        row.RelativeItem().Border(1).BorderColor(BorderColor).Background(CardBg).Padding(10).Column(c =>
                        {
                            c.Spacing(3);
                            c.Item().Text("STAY & INVOICE DETAILS").Bold().FontSize(9).FontColor(EmeraldDark);
                            c.Item().LineHorizontal(0.5f).LineColor(BorderColor);

                            c.Item().PaddingTop(2).Table(t =>
                            {
                                t.ColumnsDefinition(cd =>
                                {
                                    cd.RelativeColumn(4);
                                    cd.RelativeColumn(6);
                                });

                                AddDetailRow(t, "Invoice No", invoiceNo, isBold: true);
                                AddDetailRow(t, "Invoice Date", invoiceDate, isBold: true);
                                AddDetailRow(t, "Regn. No", regnNo);
                                AddDetailRow(t, "Room No", $"{roomNumber} ({roomType})");
                                AddDetailRow(t, "Arrival", $"{checkIn} {checkInTime}");
                                AddDetailRow(t, "Departure", $"{checkOut} {checkOutTime}");
                                AddDetailRow(t, "Total Days", $"{days} Day(s)");
                                AddDetailRow(t, "Persons", $"{persons}");
                            });
                        });
                    });

                    // ── Services & Charges Table ──
                    mainCol.Item().Border(1).BorderColor(BorderColor).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);   // Sr.
                            columns.RelativeColumn(4);   // Description
                            columns.ConstantColumn(52);   // SAC Code
                            columns.ConstantColumn(58);   // Tariff/Day
                            columns.ConstantColumn(36);   // Days
                            columns.ConstantColumn(64);   // Taxable
                            columns.ConstantColumn(46);   // CGST Rate
                            columns.ConstantColumn(52);   // CGST Amt
                            columns.ConstantColumn(46);   // SGST Rate
                            columns.ConstantColumn(52);   // SGST Amt
                            columns.ConstantColumn(64);   // Total
                        });

                        // Header Row
                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderStyle).AlignCenter().Text("Sr.");
                            h.Cell().Element(HeaderStyle).Text("Description of Services");
                            h.Cell().Element(HeaderStyle).AlignCenter().Text("SAC");
                            h.Cell().Element(HeaderStyle).AlignRight().Text("Tariff");
                            h.Cell().Element(HeaderStyle).AlignCenter().Text("Days");
                            h.Cell().Element(HeaderStyle).AlignRight().Text("Taxable");
                            h.Cell().Element(HeaderStyle).AlignCenter().Text("CGST %");
                            h.Cell().Element(HeaderStyle).AlignRight().Text("CGST ₹");
                            h.Cell().Element(HeaderStyle).AlignCenter().Text("SGST %");
                            h.Cell().Element(HeaderStyle).AlignRight().Text("SGST ₹");
                            h.Cell().Element(HeaderStyle).AlignRight().Text("Amount ₹");

                            static IContainer HeaderStyle(IContainer c) =>
                                c.Background(EmeraldDark).PaddingVertical(6).PaddingHorizontal(3)
                                 .DefaultTextStyle(x => x.Bold().FontSize(7.5f).FontColor(Colors.White));
                        });

                        // Item Row 1
                        table.Cell().Element(BodyStyle).AlignCenter().Text("1.");
                        table.Cell().Element(BodyStyle).Column(c =>
                        {
                            c.Item().Text("Room Accommodation").SemiBold().FontSize(8.5f);
                            c.Item().Text($"Room {roomNumber} - {roomType}").FontSize(7.5f).FontColor(NeutralMuted);
                        });
                        table.Cell().Element(BodyStyle).AlignCenter().Text("996311").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignRight().Text($"{tariffPerDay:N2}").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignCenter().Text($"{days}").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignRight().Text($"{taxableValue:N2}").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignCenter().Text($"{cgstRate:F2}%").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignRight().Text($"{cgstAmount:N2}").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignCenter().Text($"{sgstRate:F2}%").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignRight().Text($"{sgstAmount:N2}").FontSize(8f);
                        table.Cell().Element(BodyStyle).AlignRight().Text($"{calculatedTotal:N2}").SemiBold().FontSize(8.5f);

                        static IContainer BodyStyle(IContainer c) =>
                            c.BorderBottom(0.5f).BorderColor(BorderColor).PaddingVertical(6).PaddingHorizontal(3);
                    });

                    // ── Summary & Amount in Words Section ──
                    mainCol.Item().Row(row =>
                    {
                        // Left: Amount in words
                        row.RelativeItem(6).Column(c =>
                        {
                            c.Spacing(4);
                            c.Item().Background(CardBg).Border(1).BorderColor(BorderColor).Padding(8).Column(box =>
                            {
                                box.Item().Text("AMOUNT IN WORDS").Bold().FontSize(7.5f).FontColor(NeutralMuted);
                                box.Item().PaddingTop(2).Text(amountInWords).Bold().FontSize(8.5f).FontColor(EmeraldDark);
                            });

                            c.Item().PaddingTop(4).Text("Terms & Conditions:").Bold().FontSize(8f).FontColor(NeutralDark);
                            c.Item().Text("• Check-out time is 10:00 AM.").FontSize(7.5f).FontColor(NeutralMuted);
                            c.Item().Text("• Subject to Bhavnagar Jurisdiction.").FontSize(7.5f).FontColor(NeutralMuted);
                            c.Item().Text("• Goods & Services once billed cannot be refunded.").FontSize(7.5f).FontColor(NeutralMuted);
                            c.Item().Text("• E. & O.E.").FontSize(7.5f).FontColor(NeutralMuted);
                        });

                        row.ConstantItem(12);

                        // Right: Calculation & Total Summary Box
                        row.RelativeItem(5).Border(1).BorderColor(BorderColor).Background(CardBg).Padding(8).Column(c =>
                        {
                            c.Spacing(3);

                            SummaryLine(c, "Taxable Value", $"{taxableValue:N2}");
                            SummaryLine(c, $"CGST ({cgstRate:F2}%)", $"{cgstAmount:N2}");
                            SummaryLine(c, $"SGST ({sgstRate:F2}%)", $"{sgstAmount:N2}");
                            if (roundOff != 0)
                                SummaryLine(c, "Round Off", $"{roundOff:+0.00;-0.00;0.00}");

                            c.Item().PaddingTop(3).LineHorizontal(1).LineColor(EmeraldDark);

                            c.Item().PaddingTop(3).Background(EmeraldLight).Padding(5).Row(r =>
                            {
                                r.RelativeItem().AlignLeft().AlignMiddle().Text("INVOICE TOTAL").Bold().FontSize(10).FontColor(EmeraldDark);
                                r.RelativeItem().AlignRight().AlignMiddle().Text($"₹ {totalAmount:N2}").Bold().FontSize(12).FontColor(EmeraldDark);
                            });
                        });
                    });

                    // ── Signature & Stamp Block ──
                    mainCol.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem(6).Column(c =>
                        {
                            c.Item().Text("Thank you for staying at The Sankalp Retreat!").Italic().FontSize(8.5f).FontColor(EmeraldMedium);
                            c.Item().Text("We look forward to welcoming you again.").FontSize(7.5f).FontColor(NeutralMuted);
                        });

                        row.RelativeItem(4).Column(c =>
                        {
                            c.Item().AlignRight().Text($"For, {hotelName}").Bold().FontSize(8.5f).FontColor(NeutralDark);
                            c.Item().PaddingTop(30).AlignRight().Text("Authorised Signatory").FontSize(8f).FontColor(NeutralMuted);
                            c.Item().AlignRight().LineHorizontal(0.5f).LineColor(BorderColor);
                        });
                    });
                });
            }

            void ComposeFooter(IContainer container)
            {
                // PaddingHorizontal(15) matches page body margin — footer text aligns with invoice content.
                container.PaddingTop(8).PaddingHorizontal(15).BorderTop(0.5f).BorderColor(BorderColor).Row(row =>
                {
                    row.RelativeItem().AlignLeft().Text("This is a computer generated invoice.").FontSize(7.5f).FontColor(NeutralMuted);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Page ").FontSize(7.5f).FontColor(NeutralMuted);
                        x.CurrentPageNumber().FontSize(7.5f).FontColor(NeutralMuted);
                        x.Span(" of ").FontSize(7.5f).FontColor(NeutralMuted);
                        x.TotalPages().FontSize(7.5f).FontColor(NeutralMuted);
                    });
                });
            }

            void AddDetailRow(TableDescriptor t, string label, string val, bool isBold = false)
            {
                t.Cell().PaddingVertical(1.5f).Text($"{label} :").SemiBold().FontSize(8f).FontColor(NeutralMuted);
                var valText = t.Cell().PaddingVertical(1.5f).Text(val).FontSize(8f);
                if (isBold) valText.Bold().FontColor(NeutralDark);
            }

            void SummaryLine(ColumnDescriptor col, string label, string val)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().AlignLeft().Text(label).FontSize(8.5f).FontColor(NeutralMuted);
                    r.RelativeItem().AlignRight().Text($"₹ {val}").SemiBold().FontSize(8.5f).FontColor(NeutralDark);
                });
            }
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "ZERO";
        if (number < 0) return "MINUS " + NumberToWords(Math.Abs(number));

        string words = "";

        if (number / 10000000 > 0)
        {
            words += NumberToWords(number / 10000000) + " CRORE ";
            number %= 10000000;
        }

        if (number / 100000 > 0)
        {
            words += NumberToWords(number / 100000) + " LAKH ";
            number %= 100000;
        }

        if (number / 1000 > 0)
        {
            words += NumberToWords(number / 1000) + " THOUSAND ";
            number %= 1000;
        }

        if (number / 100 > 0)
        {
            words += NumberToWords(number / 100) + " HUNDRED ";
            number %= 100;
        }

        if (number > 0)
        {
            var unitsMap = new[] { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN", "ELEVEN", "TWELVE", "THIRTEEN", "FOURTEEN", "FIFTEEN", "SIXTEEN", "SEVENTEEN", "EIGHTEEN", "NINETEEN" };
            var tensMap = new[] { "ZERO", "TEN", "TWENTY", "THIRTY", "FORTY", "FIFTY", "SIXTY", "SEVENTY", "EIGHTY", "NINETY" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += " " + unitsMap[number % 10];
            }
        }

        return words.Trim();
    }
}
