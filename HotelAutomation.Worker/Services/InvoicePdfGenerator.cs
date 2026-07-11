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
    public Task<byte[]> GenerateInvoicePdfAsync(ScheduledJob job, CancellationToken ct = default)
    {
        // Parse parameters
        string guestName = job.GuestName;
        string roomNumber = "Unknown";
        string checkIn = "";
        string checkOut = "";
        string amount = "0.00";
        string hotelName = "Sankalp Retreat";
        string bookingRef = $"BK-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";

        try
        {
            var p = JsonSerializer.Deserialize<JsonElement>(job.ParametersJson);
            if (p.TryGetProperty("Room", out var roomProp)) roomNumber = roomProp.GetString() ?? roomNumber;
            if (p.TryGetProperty("room_number", out var roomProp2)) roomNumber = roomProp2.GetString() ?? roomNumber;
            
            if (p.TryGetProperty("CheckInDate", out var ciProp)) checkIn = ciProp.GetString() ?? "";
            if (p.TryGetProperty("check_in", out var ciProp2)) checkIn = ciProp2.GetString() ?? checkIn;
            
            if (p.TryGetProperty("CheckOutDate", out var coProp)) checkOut = coProp.GetString() ?? "";
            if (p.TryGetProperty("check_out", out var coProp2)) checkOut = coProp2.GetString() ?? checkOut;
            
            if (p.TryGetProperty("Amount", out var amtProp) && amtProp.GetString() != "0.00") amount = amtProp.GetString() ?? amount;
            if (p.TryGetProperty("total_bill", out var amtProp2)) amount = amtProp2.GetString() ?? amount;
            
            if (p.TryGetProperty("HotelName", out var hProp)) hotelName = hProp.GetString() ?? hotelName;
            
            if (p.TryGetProperty("BookingReference", out var bProp)) bookingRef = bProp.GetString() ?? bookingRef;
            if (p.TryGetProperty("booking_ref", out var bProp2)) bookingRef = bProp2.GetString() ?? bookingRef;
        }
        catch { }

        decimal totalAmount = 0m;
        decimal.TryParse(amount, out totalAmount);
        
        // Assume 3 nights for demo if dates are missing, otherwise we could parse them
        int nights = 3;
        decimal ratePerNight = totalAmount > 0 ? Math.Round(totalAmount / 1.12m / nights, 2) : 2000;
        decimal roomCharge = ratePerNight * nights;
        decimal gst = totalAmount - roomCharge;
        if (totalAmount == 0)
        {
             totalAmount = roomCharge * 1.12m;
             gst = totalAmount - roomCharge;
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Helvetica")); // Using standard font

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Thank you for staying with us! We hope to see you again.");
                    x.Span("\nPage ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });

            void ComposeHeader(IContainer container)
            {
                container.Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(hotelName).FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                        column.Item().Text("A Place to Feel at Home").FontSize(14).Italic().FontColor(Colors.Grey.Medium);
                        column.Item().Text("Gujarat, India | Phone: +91 96383 25271").FontColor(Colors.Grey.Medium);
                    });
                    row.ConstantItem(100).AlignRight().Text("INVOICE").FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                });
            }

            void ComposeContent(IContainer container)
            {
                container.PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    column.Spacing(20);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text($"Invoice No: {bookingRef}").SemiBold();
                            col.Item().Text($"Date: {DateTime.Now:dd MMM yyyy}");
                        });
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text($"Guest Name: {guestName}").SemiBold();
                            col.Item().Text($"Room: {roomNumber} (Deluxe)");
                            col.Item().Text($"Check-in: {checkIn}");
                            col.Item().Text($"Check-out: {checkOut}");
                            col.Item().Text($"Total Nights: {nights}");
                        });
                    });

                    column.Item().Element(ComposeTable);
                    
                    column.Item().AlignRight().PaddingRight(20).PaddingTop(20).Text("PAID").FontSize(30).Bold().FontColor(Colors.Green.Medium);
                });
            }

            void ComposeTable(IContainer container)
            {
                container.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(80);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Description");
                        header.Cell().Element(CellStyle).AlignRight().Text("Nights");
                        header.Cell().Element(CellStyle).AlignRight().Text("Rate");
                        header.Cell().Element(CellStyle).AlignRight().Text("Amount");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                        }
                    });

                    table.Cell().Element(CellStyle).Text("Room Charge (Deluxe Room)");
                    table.Cell().Element(CellStyle).AlignRight().Text(nights.ToString());
                    table.Cell().Element(CellStyle).AlignRight().Text($"Rs. {ratePerNight:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"Rs. {roomCharge:N2}");

                    table.Cell().Element(CellStyle).Text("GST (12%)");
                    table.Cell().Element(CellStyle).AlignRight().Text("");
                    table.Cell().Element(CellStyle).AlignRight().Text("");
                    table.Cell().Element(CellStyle).AlignRight().Text($"Rs. {gst:N2}");

                    table.Cell().ColumnSpan(3).Element(CellStyle).AlignRight().Text("Total").SemiBold();
                    table.Cell().Element(CellStyle).AlignRight().Text($"Rs. {totalAmount:N2}").SemiBold();

                    static IContainer CellStyle(IContainer container)
                    {
                        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                });
            }
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        return Task.FromResult(ms.ToArray());
    }
}
