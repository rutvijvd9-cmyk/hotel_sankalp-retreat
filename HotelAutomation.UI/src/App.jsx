import React, { useState, useEffect } from 'react';
import { LayoutDashboard, MessagesSquare, Workflow, ScrollText, Send, Hotel, CheckCircle2, AlertCircle, Clock, Users, Upload, Plus } from 'lucide-react';

const API_BASE = '/api';

export default function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [stats, setStats] = useState({ templates: 0, rules: 0, outbox: 0, inbox: 0 });
  const [outbox, setOutbox] = useState([]);
  const [inbox, setInbox] = useState([]);
  const [categories, setCategories] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [logs, setLogs] = useState([]);
  const [templatesList, setTemplatesList] = useState([]);
  const [rulesList, setRulesList] = useState([]);
  const [selectedCategory, setSelectedCategory] = useState('');
  const [isFiring, setIsFiring] = useState(false);
  const [testPhoneNumber, setTestPhoneNumber] = useState('+15556585587');

  useEffect(() => {
    fetchData();
  }, [activeTab]);

  const fetchData = async () => {
    try {
      const outboxRes = await fetch(`${API_BASE}/outbox`).catch(() => null);
      if (outboxRes) {
        const outboxData = await outboxRes.json();
        setOutbox(outboxData);
        setStats(s => ({ ...s, outbox: outboxData.length }));
      }
      
      const rulesRes = await fetch(`${API_BASE}/rules`).catch(() => null);
      if (rulesRes) {
        const rulesData = await rulesRes.json();
        setRulesList(rulesData);
        setStats(s => ({ ...s, rules: rulesData.length }));
      }

      const templatesRes = await fetch(`${API_BASE}/templates`).catch(() => null);
      if (templatesRes) {
        const templatesData = await templatesRes.json();
        setTemplatesList(templatesData);
        setStats(s => ({ ...s, templates: templatesData.length }));
      }
      
      const logsRes = await fetch(`${API_BASE}/logs`).catch(() => null);
      if (logsRes) setLogs(await logsRes.json());
      
      const inboxRes = await fetch(`${API_BASE}/inbox`).catch(() => null);
      if (inboxRes) {
        const inboxData = await inboxRes.json();
        setInbox(inboxData);
        setStats(s => ({ ...s, inbox: inboxData.length }));
      }

      const catRes = await fetch(`${API_BASE}/categories`).catch(() => null);
      if (catRes) setCategories(await catRes.json());
      
      const conUrl = selectedCategory ? `${API_BASE}/contacts?categoryId=${selectedCategory}` : `${API_BASE}/contacts`;
      const conRes = await fetch(conUrl).catch(() => null);
      if (conRes) setContacts(await conRes.json());
    } catch (e) {
      console.error(e);
    }
  };

  const handleTestEvent = async () => {
    setIsFiring(true);
    try {
      await fetch(`${API_BASE}/test-event?phone=${encodeURIComponent(testPhoneNumber)}`, { method: 'POST' });
      await new Promise(r => setTimeout(r, 1000));
      await fetchData();
    } catch (e) {
      alert('Ensure the C# Backend API is running!');
    }
    setIsFiring(false);
  };

  const handleTestReply = async () => {
    try {
      await fetch(`${API_BASE}/webhooks/whatsapp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ From: '+1234567890', Body: 'Yes, please book the spa!' })
      });
      await new Promise(r => setTimeout(r, 1000));
      await fetchData();
    } catch (e) {
      alert('Ensure the C# Backend API is running!');
    }
  };

  const handleSimulateWebhook = async (status) => {
    const msg = outbox.find(m => m.Status === 'Sent' || m.status === 'Sent' || m.Status === 'Delivered' || m.status === 'Delivered');
    if (!msg) {
        alert("No suitable Sent/Delivered message found in outbox!");
        return;
    }
    const wamid = msg.ExternalMessageId || msg.externalMessageId || msg.Id || msg.id;
    try {
      await fetch(`${API_BASE}/test-webhook?wamid=${encodeURIComponent(wamid)}&status=${status}`, { method: 'POST' });
      await new Promise(r => setTimeout(r, 1000));
      await fetchData();
    } catch (e) {
      alert('Ensure the C# Backend API is running!');
    }
  };

  const renderContent = () => {
    if (activeTab === 'dashboard') {
      return (
        <div>
          <div style={{ display: 'flex', gap: '24px', marginBottom: '32px' }}>
            <div className="card" style={{ flex: 1 }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px' }}>Total Messages</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{stats.outbox}</div>
            </div>
            <div className="card" style={{ flex: 1 }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px' }}>Active Rules</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{stats.rules}</div>
            </div>
            <div className="card" style={{ flex: 1 }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px' }}>Inbox Replies</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{stats.inbox}</div>
            </div>
          </div>

          <div style={{ display: 'flex', gap: '24px', marginBottom: '32px' }}>
            <div className="card" style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div>
                <h3 style={{ marginBottom: '8px' }}>PMS CheckOut Simulation</h3>
                <p style={{ color: 'var(--text-secondary)' }}>Fires event for the given number. Starts the marketing drip campaign.</p>
                <input 
                  type="text" 
                  value={testPhoneNumber} 
                  onChange={e => setTestPhoneNumber(e.target.value)} 
                  style={{ marginTop: '8px', padding: '8px', width: '200px', borderRadius: '4px', border: '1px solid #ccc', color: '#000' }} 
                />
              </div>
              <button className="btn btn-primary" onClick={handleTestEvent} disabled={isFiring}>
                <Send size={18} /> Trigger Event
              </button>
            </div>
            
            <div className="card" style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <div>
                <h3 style={{ marginBottom: '8px' }}>Analytics Webhook Simulator</h3>
                <p style={{ color: 'var(--text-secondary)' }}>Simulates Meta sending Delivery/Read receipts.</p>
              </div>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button className="btn btn-primary" onClick={() => handleSimulateWebhook('Delivered')} style={{ backgroundColor: '#10b981' }}>
                  Simulate Delivered
                </button>
                <button className="btn btn-primary" onClick={() => handleSimulateWebhook('Read')} style={{ backgroundColor: '#3b82f6' }}>
                  Simulate Read
                </button>
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', gap: '24px' }}>
            <div className="card" style={{ flex: 1 }}>
              <h3 className="card-title">Recent Outbox</h3>
              {outbox.length === 0 ? (
                <div className="empty-state">No outbox messages found.</div>
              ) : (
                <div className="data-table-container">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Phone Number</th>
                        <th>Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {outbox.map(msg => (
                        <tr key={msg.Id || msg.id}>
                          <td style={{ fontFamily: 'monospace' }}>{msg.ToPhoneNumber || msg.toPhoneNumber}</td>
                          <td>
                            <span className={`badge badge-${(msg.Status || msg.status || 'unknown').toLowerCase()}`}>
                              {(msg.Status || msg.status) === 'Sent' && <CheckCircle2 size={12} style={{marginRight:'4px', verticalAlign:'middle'}}/>}
                              {(msg.Status || msg.status) === 'Failed' && <AlertCircle size={12} style={{marginRight:'4px', verticalAlign:'middle'}}/>}
                              {(msg.Status || msg.status) === 'Pending' && <Clock size={12} style={{marginRight:'4px', verticalAlign:'middle'}}/>}
                              {msg.Status || msg.status}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="card" style={{ flex: 1 }}>
              <h3 className="card-title">Recent Inbox Replies</h3>
              {inbox.length === 0 ? (
                <div className="empty-state">No replies received yet.</div>
              ) : (
                <div className="data-table-container">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>From</th>
                        <th>Message</th>
                      </tr>
                    </thead>
                    <tbody>
                      {inbox.map(msg => (
                        <tr key={msg.Id || msg.id}>
                          <td style={{ fontFamily: 'monospace' }}>{msg.FromPhoneNumber || msg.fromPhoneNumber}</td>
                          <td>{msg.Content || msg.content}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </div>
      );
    }

    if (activeTab === 'contacts') {
      const handleImport = async (e) => {
        const file = e.target.files[0];
        if (!file) return;
        const formData = new FormData();
        formData.append('file', file);
        if (selectedCategory) formData.append('categoryId', selectedCategory);
        
        try {
          const res = await fetch(`${API_BASE}/contacts/import`, { method: 'POST', body: formData });
          const data = await res.json();
          alert(`Imported ${data.importedCount} contacts!`);
          fetchData();
        } catch (err) {
          alert("Import failed!");
        }
      };

      const handleAddCategory = async () => {
        const name = prompt("Enter new category name:");
        if (!name) return;
        await fetch(`${API_BASE}/categories`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name })
        });
        fetchData();
      };

      const dynamicHeaders = Array.from(new Set(contacts.flatMap(c => {
        try { return Object.keys(JSON.parse(c.CustomFieldsJson || c.customFieldsJson || '{}')); } catch(e) { return []; }
      })));

      return (
        <div>
          <div style={{ display: 'flex', gap: '16px', marginBottom: '24px', alignItems: 'center' }}>
            <select 
              value={selectedCategory} 
              onChange={e => { setSelectedCategory(e.target.value); setTimeout(fetchData, 100); }} 
              style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
            >
              <option value="">All Categories</option>
              {categories.map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
            </select>
            <button className="btn btn-secondary" onClick={handleAddCategory}>
              <Plus size={16} /> New Category
            </button>
            <div style={{ flex: 1 }}></div>
            <label className="btn btn-primary" style={{ cursor: 'pointer', backgroundColor: '#10b981' }}>
              <Upload size={16} /> Import CSV
              <input type="file" accept=".csv" style={{ display: 'none' }} onChange={handleImport} />
            </label>
          </div>

          <div className="card">
            <h3 className="card-title">Contacts Directory</h3>
            {contacts.length === 0 ? (
              <div className="empty-state">No contacts found. Try importing a CSV!</div>
            ) : (
              <div className="data-table-container">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Phone</th>
                      <th>First Name</th>
                      <th>Last Name</th>
                      {dynamicHeaders.map(h => <th key={h}>{h}</th>)}
                    </tr>
                  </thead>
                  <tbody>
                    {contacts.map(c => {
                      let fields = {};
                      try { fields = JSON.parse(c.CustomFieldsJson || c.customFieldsJson || '{}'); } catch(e){}
                      return (
                        <tr key={c.Id || c.id}>
                          <td style={{ fontFamily: 'monospace' }}>{c.PhoneNumber || c.phoneNumber}</td>
                          <td>{c.FirstName || c.firstName}</td>
                          <td>{c.LastName || c.lastName}</td>
                          {dynamicHeaders.map(h => <td key={h}>{fields[h] || '-'}</td>)}
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      );
    }

    if (activeTab === 'templates') {
      return (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
            <h3 className="card-title">Message Templates</h3>
            <button className="btn btn-primary" onClick={() => alert('Template Builder coming soon!')}>
              <Plus size={16} /> Create Template
            </button>
          </div>
          <div className="data-table-container">
            <table className="data-table">
              <thead><tr><th>Name</th><th>Subject</th><th>Channel</th></tr></thead>
              <tbody>
                {templatesList.map(t => (
                  <tr key={t.Id || t.id}>
                    <td>{t.Name || t.name}</td>
                    <td>{t.Subject || t.subject || '-'}</td>
                    <td><span className="badge badge-sent">WhatsApp</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      );
    }

    if (activeTab === 'rules') {
      return (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
            <h3 className="card-title">Automation Rules</h3>
            <button className="btn btn-primary" onClick={() => alert('Rule Builder coming soon!')}>
              <Plus size={16} /> Create Rule
            </button>
          </div>
          <div className="data-table-container">
            <table className="data-table">
              <thead><tr><th>Name</th><th>Trigger</th><th>Action</th><th>Status</th></tr></thead>
              <tbody>
                {rulesList.map(r => (
                  <tr key={r.Id || r.id}>
                    <td>{r.Name || r.name}</td>
                    <td><span className="badge badge-pending">{r.TriggerType || r.triggerType}</span></td>
                    <td>{r.ActionType || r.actionType}</td>
                    <td><span className="badge badge-sent">Active</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      );
    }

    if (activeTab === 'logs') {
      return (
        <div className="card">
          <h3 className="card-title">Event Logs</h3>
          <div className="data-table-container">
            <table className="data-table">
              <thead><tr><th>Occurred At</th><th>Event Type</th><th>Payload</th></tr></thead>
              <tbody>
                {logs.map(l => (
                  <tr key={l.Id || l.id}>
                    <td style={{ whiteSpace: 'nowrap' }}>{new Date(l.OccurredAt || l.occurredAt).toLocaleString()}</td>
                    <td><span className="badge badge-delivered">{l.EventType || l.eventType}</span></td>
                    <td style={{ fontFamily: 'monospace', fontSize: '0.85em', maxWidth: '400px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {l.RawPayloadJson || l.rawPayloadJson}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      );
    }

    return null;
  };

  return (
    <div className="app-container">
      <aside className="sidebar">
        <div className="sidebar-header">
          <Hotel color="var(--primary)" /> HotelAuto
        </div>
        <nav style={{ display: 'flex', flexDirection: 'column', gap: '8px', padding: '12px 0' }}>
          <div className={`nav-item ${activeTab === 'dashboard' ? 'active' : ''}`} onClick={() => setActiveTab('dashboard')}>
            <LayoutDashboard size={20} /> Dashboard
          </div>
          <div className={`nav-item ${activeTab === 'contacts' ? 'active' : ''}`} onClick={() => setActiveTab('contacts')}>
            <Users size={20} /> Contacts
          </div>
          <div className={`nav-item ${activeTab === 'templates' ? 'active' : ''}`} onClick={() => setActiveTab('templates')}>
            <MessagesSquare size={20} /> Templates
          </div>
          <div className={`nav-item ${activeTab === 'rules' ? 'active' : ''}`} onClick={() => setActiveTab('rules')}>
            <Workflow size={20} /> Automation Rules
          </div>
          <div className={`nav-item ${activeTab === 'logs' ? 'active' : ''}`} onClick={() => setActiveTab('logs')}>
            <ScrollText size={20} /> Event Logs
          </div>
        </nav>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div className="topbar-title">
            {activeTab.charAt(0).toUpperCase() + activeTab.slice(1)}
          </div>
        </header>

        <div className="page-content">
          {renderContent()}
        </div>
      </main>
    </div>
  );
}
