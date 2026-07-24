# Hotel Automation Webhook API Documentation

This document outlines the API required to push Property Management System (PMS) events (such as Check-Ins and Check-Outs) to our Hotel Automation Software. 

By sending a standard HTTP POST request to our webhook URL, your software can instantly notify us when a guest checks in or checks out, allowing us to trigger automated WhatsApp messages.

---

## 1. Endpoint Details

- **HTTP Method:** `POST`
- **URL:** `http://localhost:8000/api/webhooks/pms-events` *(Note: Port 8000 is the default, but this can be adjusted if needed)*
- **Headers Required:**
  - `Content-Type: application/json`
  - `Authorization: Bearer <Your-Secret-Token>` *(A security token we will provide to ensure only authorized systems can send us data)*

---

## 2. Event Payload Schema

When an event occurs in your system, send a JSON payload containing the following fields:

| Field | Type | Required | Description |
|---|---|---|---|
| `eventType` | String | **Yes** | The type of event. Must be either `"PMS_CHECKIN"` or `"PMS_CHECKOUT"`. |
| `guestName` | String | **Yes** | The full name of the guest. |
| `phoneNumber` | String | **Yes** | The guest's phone number including the country code (e.g., `+919876543210`). |
| `roomNumber` | String | **Yes** | The room number assigned to the guest. |
| `amount` | Number | No | The total bill amount (useful for Check-Out events to send the final bill). |
| `timestamp` | String | **Yes** | ISO 8601 formatted date and time when the event occurred (e.g., `2026-07-15T14:30:00Z`). |
| `pdfBase64` | String | No | Base64-encoded PDF bill/invoice string. Allows direct delivery of generated PDF bills to guest's WhatsApp in offline modes. |
| `billPdfBase64` | String | No | Alternate name for `pdfBase64` (alias). |

*Note: Any additional fields sent in the payload (like `Bill No`, `Discount`, etc.) will be automatically stored as optional pass-through data in the guest profile.*

---

## 3. Examples

### Example 1: Guest Check-In Payload & Curl Command

**JSON Payload:**
```json
{
  "eventType": "PMS_CHECKIN",
  "guestName": "Rutvij Test Guest",
  "phoneNumber": "+918460998080",
  "roomNumber": "1006",
  "amount": 2300.00,
  "timestamp": "2026-07-16T15:45:00Z",
  "Registration Number": "981",
  "Bill No": "ROOM/975",
  "Arrival Date": "13/07/2026",
  "Departure date": "14/07/2026",
  "Arrival Time": "17:50",
  "Departure Time": "09:31",
  "Company Name": "DIRECT",
  "Group": "G-1",
  "City Name": "BAREILLY",
  "State Name": "UTTAR PRADESH",
  "Prefix": "Mr",
  "Nationality": "Indian",
  "Room No": "1006",
  "Room Type": "PREMIUM ROOM",
  "Person": "2",
  "Extra Person": "0",
  "Expected Stay days": "1",
  "Expected Departure Date": "14/07/2026",
  "AP": "false",
  "MAP": "false",
  "EP": "true",
  "CP": "false",
  "Discount": "4310.00",
  "Room Tariff": "6500.00",
  "Final Tariff": "2190.00",
  "Total": "2190.00",
  "Carry Date": "13/07/2026",
  "Carry registraction No": "CR-981",
  "Carry Amount": "0.00",
  "Deposit": "0.00",
  "Reservation Remark": "N/A",
  "Room Change": "None",
  "Other Remark": "Regular Guest",
  "days": "1",
  "CGST": "54.75",
  "SGST": "54.75",
  "Round off": "0.50",
  "Mode of Payment/Bill To": "CASH ON HAND",
  "Room Total": "2300.00",
  "C/F. Amount": "0.00",
  "Deposit(-)": "0.00",
  "Roon Service": "0.00",
  "Laundary": "0.00",
  "Misc Service": "0.00",
  "Final Amount": "2300.00",
  "B/F Registration No": "BF-0",
  "C/F Registration No": "CF-0"
}
```

**Curl Command:**
```bash
curl -X POST http://localhost:8000/api/webhooks/pms-events \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sankalp_retreat_pms_secret_2026" \
  -d '{
    "eventType": "PMS_CHECKIN",
    "guestName": "Rutvij Test Guest",
    "phoneNumber": "+918460998080",
    "roomNumber": "1006",
    "amount": 2300.00,
    "timestamp": "2026-07-16T15:45:00Z",
    "Registration Number": "981",
    "Bill No": "ROOM/975",
    "Arrival Date": "13/07/2026",
    "Departure date": "14/07/2026",
    "Arrival Time": "17:50",
    "Departure Time": "09:31",
    "Company Name": "DIRECT",
    "Group": "G-1",
    "City Name": "BAREILLY",
    "State Name": "UTTAR PRADESH",
    "Prefix": "Mr",
    "Nationality": "Indian",
    "Room No": "1006",
    "Room Type": "PREMIUM ROOM",
    "Person": "2",
    "Extra Person": "0",
    "Expected Stay days": "1",
    "Expected Departure Date": "14/07/2026",
    "AP": "false",
    "MAP": "false",
    "EP": "true",
    "CP": "false",
    "Discount": "4310.00",
    "Room Tariff": "6500.00",
    "Final Tariff": "2190.00",
    "Total": "2190.00",
    "Carry Date": "13/07/2026",
    "Carry registraction No": "CR-981",
    "Carry Amount": "0.00",
    "Deposit": "0.00",
    "Reservation Remark": "N/A",
    "Room Change": "None",
    "Other Remark": "Regular Guest",
    "days": "1",
    "CGST": "54.75",
    "SGST": "54.75",
    "Round off": "0.50",
    "Mode of Payment/Bill To": "CASH ON HAND",
    "Room Total": "2300.00",
    "C/F. Amount": "0.00",
    "Deposit(-)": "0.00",
    "Roon Service": "0.00",
    "Laundary": "0.00",
    "Misc Service": "0.00",
    "Final Amount": "2300.00",
    "B/F Registration No": "BF-0",
    "C/F Registration No": "CF-0"
  }'
```

### Example 2: Guest Check-Out Payload & Curl Command

**JSON Payload:**
```json
{
  "eventType": "PMS_CHECKOUT",
  "guestName": "Rutvij Test Guest",
  "phoneNumber": "+918460998080",
  "roomNumber": "1006",
  "amount": 2300.00,
  "timestamp": "2026-07-16T15:53:00Z",
  "pdfBase64": "JVBERi0xLjEKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvUmVzb3VyY2VzIDQgMCBSIC9NZWRpYUJveCBbMCAwIDYxMiA3OTJdIC9Db250ZW50cyA1IDAgUiA+PgplbmRvYmoKNCAwIG9iago8PCAvRm9udCA8PCAvRjEgPDwgL1R5cGUgL0ZvbnQgL1N1YnR5cGUgL1R5cGUxIC9CYXNlRm9udCAvSGVsdmV0aWNhID4+ID4+ID4+CmVuZG9iago1IDAgb2JqCjw8IC9MZW5ndGggNzggPj4Kc3RyZWFtCkJUIC9GMSAxNiBUZiA1MCA3MDAgVGQgKEhlbGxvIFJ1dHZpaiwgdGhpcyBpcyBhIHZhbGlkIHRlc3QgUERGIGludm9pY2UhKSBUaiBFVAplbmRzdHJlYW0KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZgowMDAwMDAwMDA5IDAwMDAwIG4KMDAwMDAwMDA1OCAwMDAwMCBuCjAwMDAwMDAxMTUgMDAwMDAgbgowMDAwMDAwMjE5IDAwMDAwIG4KMDAwMDAwMDMxMSAwMDAwMCBuCnRyYWlsZXIKPDwgL1Jvb3QgMSAwIFIgL1NpemUgNiA+PgpzdGFydHhyZWYKNDM5CiUlRU9G",
  "Registration Number": "981",
  "Bill No": "ROOM/975",
  "Arrival Date": "13/07/2026",
  "Departure date": "14/07/2026",
  "Arrival Time": "17:50",
  "Departure Time": "09:31",
  "Company Name": "DIRECT",
  "Group": "G-1",
  "City Name": "BAREILLY",
  "State Name": "UTTAR PRADESH",
  "Prefix": "Mr",
  "Nationality": "Indian",
  "Room No": "1006",
  "Room Type": "PREMIUM ROOM",
  "Person": "2",
  "Extra Person": "0",
  "Expected Stay days": "1",
  "Expected Departure Date": "14/07/2026",
  "AP": "false",
  "MAP": "false",
  "EP": "true",
  "CP": "false",
  "Discount": "4310.00",
  "Room Tariff": "6500.00",
  "Final Tariff": "2190.00",
  "Total": "2190.00",
  "Carry Date": "13/07/2026",
  "Carry registraction No": "CR-981",
  "Carry Amount": "0.00",
  "Deposit": "0.00",
  "Reservation Remark": "Checkout Complete",
  "Room Change": "None",
  "Other Remark": "Payment Settled via Cash",
  "days": "1",
  "CGST": "54.75",
  "SGST": "54.75",
  "Round off": "0.50",
  "Mode of Payment/Bill To": "CASH ON HAND",
  "Room Total": "2300.00",
  "C/F. Amount": "100.00",
  "Deposit(-)": "0.00",
  "Roon Service": "250.00",
  "Laundary": "120.00",
  "Misc Service": "0.00",
  "Final Amount": "2670.00",
  "B/F Registration No": "BF-0",
  "C/F Registration No": "CF-0"
}
```

**Curl Command:**
```bash
curl -X POST http://localhost:8000/api/webhooks/pms-events \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sankalp_retreat_pms_secret_2026" \
  -d '{
    "eventType": "PMS_CHECKOUT",
    "guestName": "Rutvij Test Guest",
    "phoneNumber": "+918460998080",
    "roomNumber": "1006",
    "amount": 2300.00,
    "timestamp": "2026-07-16T15:53:00Z",
    "pdfBase64": "JVBERi0xLjEKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvUmVzb3VyY2VzIDQgMCBSIC9NZWRpYUJveCBbMCAwIDYxMiA3OTJdIC9Db250ZW50cyA1IDAgUiA+PgplbmRvYmoKNCAwIG9iago8PCAvRm9udCA8PCAvRjEgPDwgL1R5cGUgL0ZvbnQgL1N1YnR5cGUgL1R5cGUxIC9CYXNlRm9udCAvSGVsdmV0aWNhID4+ID4+ID4+CmVuZG9iago1IDAgb2JqCjw8IC9MZW5ndGggNzggPj4Kc3RyZWFtCkJUIC9GMSAxNiBUZiA1MCA3MDAgVGQgKEhlbGxvIFJ1dHZpaiwgdGhpcyBpcyBhIHZhbGlkIHRlc3QgUERGIGludm9pY2UhKSBUaiBFVAplbmRzdHJlYW0KZW5kb2JqCnhyZWYKMCA2CjAwMDAwMDAwMDAgNjU1MzUgZgowMDAwMDAwMDA5IDAwMDAwIG4KMDAwMDAwMDA1OCAwMDAwMCBuCjAwMDAwMDAxMTUgMDAwMDAgbgowMDAwMDAwMjE5IDAwMDAwIG4KMDAwMDAwMDMxMSAwMDAwMCBuCnRyYWlsZXIKPDwgL1Jvb3QgMSAwIFIgL1NpemUgNiA+PgpzdGFydHhyZWYKNDM5CiUlRU9G",
    "Registration Number": "981",
    "Bill No": "ROOM/975",
    "Arrival Date": "13/07/2026",
    "Departure date": "14/07/2026",
    "Arrival Time": "17:50",
    "Departure Time": "09:31",
    "Company Name": "DIRECT",
    "Group": "G-1",
    "City Name": "BAREILLY",
    "State Name": "UTTAR PRADESH",
    "Prefix": "Mr",
    "Nationality": "Indian",
    "Room No": "1006",
    "Room Type": "PREMIUM ROOM",
    "Person": "2",
    "Extra Person": "0",
    "Expected Stay days": "1",
    "Expected Departure Date": "14/07/2026",
    "AP": "false",
    "MAP": "false",
    "EP": "true",
    "CP": "false",
    "Discount": "4310.00",
    "Room Tariff": "6500.00",
    "Final Tariff": "2190.00",
    "Total": "2190.00",
    "Carry Date": "13/07/2026",
    "Carry registraction No": "CR-981",
    "Carry Amount": "0.00",
    "Deposit": "0.00",
    "Reservation Remark": "Checkout Complete",
    "Room Change": "None",
    "Other Remark": "Payment Settled via Cash",
    "days": "1",
    "CGST": "54.75",
    "SGST": "54.75",
    "Round off": "0.50",
    "Mode of Payment/Bill To": "CASH ON HAND",
    "Room Total": "2300.00",
    "C/F. Amount": "100.00",
    "Deposit(-)": "0.00",
    "Roon Service": "250.00",
    "Laundary": "120.00",
    "Misc Service": "0.00",
    "Final Amount": "2670.00",
    "B/F Registration No": "BF-0",
    "C/F Registration No": "CF-0"
  }'
```

---

## 4. Sending PDF Invoices (Base64 Encoding)

For local/offline environments where public URLs to invoices cannot be shared, the Property Management System (PMS) should convert the generated invoice PDF into a **Base64-encoded string** and send it inside the request under the `pdfBase64` property. 

Our server will decode the Base64 stream, upload the raw file directly to Meta's WhatsApp servers, and deliver it as a document message to the guest.

### Code Examples to Convert PDF to Base64:

*   **C# (.NET):**
    ```csharp
    byte[] pdfBytes = File.ReadAllBytes("invoice.pdf");
    string base64String = Convert.ToBase64String(pdfBytes);
    ```
*   **Java / Kotlin:**
    ```java
    byte[] fileContent = Files.readAllBytes(Paths.get("invoice.pdf"));
    String base64String = Base64.getEncoder().encodeToString(fileContent);
    ```
*   **Node.js (JavaScript):**
    ```javascript
    const fs = require('fs');
    const base64String = fs.readFileSync('invoice.pdf').toString('base64');
    ```
*   **Python:**
    ```python
    import base64
    with open("invoice.pdf", "rb") as f:
        base64_string = base64.b64encode(f.read()).decode('utf-8')
    ```

---

## 5. Expected Responses

Our server will respond with standard HTTP status codes:

- `202 Accepted`: The event was successfully received and queued for processing asynchronously.
- `400 Bad Request`: The JSON payload was invalid or missing required fields.
- `401 Unauthorized`: The authorization token is missing or incorrect.
- `500 Internal Server Error`: An unexpected error occurred on our end.

**Example Success Response (202):**
```json
{
  "status": "success",
  "message": "Event PMS_CHECKIN for room 1006 processed successfully."
}
```

**Example Error Response (400):**
```json
{
  "status": "error",
  "message": "Missing required fields (eventType, guestName, phoneNumber)"
}
```

---

## 6. REST Best-Practice Recommendations (Optional)

To ensure the highest reliability when integrating with our API, we recommend the following:
- **HTTPS Only:** Always send webhooks over a secure HTTPS connection.
- **Idempotency:** Consider sending a unique transaction or event ID if applicable, to safely retry on timeouts.
- **Retries:** Implement exponential backoff for failed requests (e.g. 500-level errors), but do not retry 400 or 401 responses automatically without correction.
- **Async Handling:** Since our API returns `202 Accepted`, your system should not wait for the actual WhatsApp message to be delivered before unblocking your local checkout process.
```
