const express = require('express');
const app = express();
const port = process.env.PORT || 3000;

// Rolling history to hold incoming webhook payloads
let webhookHistory = [];
let legacyQueue = [];
let nextMessageId = 1;
const MAX_HISTORY = 500;

app.use(express.json());

// 1. Meta Webhook Verification (GET)
app.get('/api/webhooks/whatsapp', (req, res) => {
    const mode = req.query['hub.mode'];
    const token = req.query['hub.verify_token'];
    const challenge = req.query['hub.challenge'];

    const expectedToken = process.env.VERIFY_TOKEN || 'sankalp_retreat_verify_token';

    if (mode && token) {
        if (mode === 'subscribe' && token === expectedToken) {
            console.log('WEBHOOK_VERIFIED');
            return res.status(200).send(challenge);
        } else {
            return res.sendStatus(403);
        }
    }
    return res.status(400).send('Bad Request');
});

// 2. Receive Webhook from Meta (POST)
app.post('/api/webhooks/whatsapp', (req, res) => {
    const body = req.body;
    
    // Validate that it's a WhatsApp webhook
    if (body.object === 'whatsapp_business_account') {
        console.log(`Received payload with ${body.entry?.length || 0} entries.`);
        webhookHistory.push({ id: nextMessageId++, payload: body });
        legacyQueue.push(body);
        if (webhookHistory.length > MAX_HISTORY) {
            webhookHistory.shift(); // Keep only the last MAX_HISTORY items
        }
        if (legacyQueue.length > MAX_HISTORY) {
            legacyQueue.shift();
        }
        return res.status(200).send('EVENT_RECEIVED');
    }
    
    return res.sendStatus(404);
});

// 3. Endpoint for the local PC to poll and retrieve the queued webhooks
app.get('/api/webhooks/poll', (req, res) => {
    // Backward compatibility for old clients not sending last_id
    if (req.query.last_id === undefined) {
        const currentQueue = legacyQueue;
        legacyQueue = [];
        return res.json(currentQueue);
    }

    const lastId = parseInt(req.query.last_id || '0', 10);
    
    // Return all messages strictly newer than the requested last_id
    const newMessages = webhookHistory.filter(msg => msg.id > lastId);
    
    res.json(newMessages);
});

// Simple health check for Render dashboard
app.get('/', (req, res) => res.send('Hotel Automation Webhook Relay is running!'));

// Ping endpoint to keep the server awake and confirm it's live
app.get('/ping', (req, res) => res.status(200).send('live'));

app.listen(port, () => {
    console.log(`Webhook relay server listening at http://localhost:${port}`);
});
