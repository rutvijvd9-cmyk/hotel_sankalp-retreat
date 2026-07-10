const express = require('express');
const app = express();
const port = process.env.PORT || 3000;

// In-memory queue to hold incoming webhook payloads until the local server polls them
let webhookQueue = [];

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
        console.log(`Received payload with ${body.entry?.length || 0} entries. Queue size: ${webhookQueue.length + 1}`);
        webhookQueue.push(body);
        return res.status(200).send('EVENT_RECEIVED');
    }
    
    return res.sendStatus(404);
});

// 3. Endpoint for the local PC to poll and retrieve the queued webhooks
app.get('/api/webhooks/poll', (req, res) => {
    // Return the current queue and atomically clear it
    const currentQueue = webhookQueue;
    webhookQueue = [];
    
    res.json(currentQueue);
});

// Simple health check for Render dashboard
app.get('/', (req, res) => res.send('Hotel Automation Webhook Relay is running!'));

app.listen(port, () => {
    console.log(`Webhook relay server listening at http://localhost:${port}`);
});
