import React, { useState, useEffect } from 'react';
import { LayoutDashboard, MessagesSquare, Workflow, ScrollText, Send, Hotel, CheckCircle2, AlertCircle, Clock, Users, Upload, Plus, Megaphone, Edit2, Trash2, Settings } from 'lucide-react';

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
  const [broadcasts, setBroadcasts] = useState([]);
  const [selectedBroadcastCategory, setSelectedBroadcastCategory] = useState('');
  const [selectedBroadcastTemplate, setSelectedBroadcastTemplate] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [isFiring, setIsFiring] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [testPhoneNumber, setTestPhoneNumber] = useState('+15556585587');
  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [showColumnModal, setShowColumnModal] = useState(false);
  const [isEditingTemplate, setIsEditingTemplate] = useState(false);
  const [editingTemplateId, setEditingTemplateId] = useState(null);
  const [newTemplate, setNewTemplate] = useState({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp' });

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
      
      if (activeTab === 'templates' || activeTab === 'broadcasts') {
        const templatesRes = await fetch(`${API_BASE}/templates`).catch(() => null);
        if (templatesRes) setTemplatesList(await templatesRes.json());
      }
      
      const rulesRes = await fetch(`${API_BASE}/rules`).catch(() => null);
      if (rulesRes) {
        const rulesData = await rulesRes.json();
        setRulesList(rulesData);
        setStats(s => ({ ...s, rules: rulesData.length }));
      }
      
      if (activeTab === 'logs') {
        const logsRes = await fetch(`${API_BASE}/logs`).catch(() => null);
        if (logsRes) setLogs(await logsRes.json());
      }

      if (activeTab === 'broadcasts') {
        const bRes = await fetch(`${API_BASE}/broadcast`).catch(() => null);
        if (bRes) setBroadcasts(await bRes.json());
        if (categories.length === 0) {
          const catRes = await fetch(`${API_BASE}/categories`).catch(() => null);
          if (catRes) setCategories(await catRes.json());
        }
      }
      
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
        
        setIsImporting(true);
        try {
          const res = await fetch(`${API_BASE}/contacts/import`, { method: 'POST', body: formData });
          if (!res.ok) throw new Error('Upload failed');
          const data = await res.json();
          alert(`Success! Imported ${data.importedCount} contacts.`);
          fetchData();
        } catch (err) {
          alert("Import failed! Ensure the file is a valid CSV.");
        } finally {
          setIsImporting(false);
          e.target.value = null;
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

      const handleDeleteColumn = async (colName) => {
        if (!window.confirm(`Are you sure you want to permanently delete the column "${colName}" and all its data from ALL contacts?`)) return;
        try {
          const res = await fetch(`${API_BASE}/contacts/columns/${encodeURIComponent(colName)}`, { method: 'DELETE' });
          if (res.ok) {
            alert(`Column ${colName} deleted successfully.`);
            fetchData();
          } else {
            alert("Failed to delete column.");
          }
        } catch (err) {
          alert("Error deleting column.");
        }
      };

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
            <button className="btn btn-secondary" onClick={() => setShowColumnModal(true)}>
              <Settings size={16} /> Manage Columns
            </button>
            <label className="btn btn-primary" style={{ cursor: isImporting ? 'not-allowed' : 'pointer', backgroundColor: isImporting ? '#94a3b8' : '#10b981' }}>
              {isImporting ? 'Importing...' : <><Upload size={16} /> Import CSV</>}
              <input type="file" accept=".csv" style={{ display: 'none' }} onChange={handleImport} disabled={isImporting} />
            </label>
          </div>

          {showColumnModal && (
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '400px' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>Manage Custom Columns</h3>
                <p style={{ fontSize: '0.9em', color: '#64748b', marginBottom: '16px' }}>
                  These columns were generated dynamically from your CSV imports. Deleting a column here will remove it from all contacts permanently.
                </p>
                {dynamicHeaders.length === 0 ? (
                  <div className="empty-state">No custom columns found.</div>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', maxHeight: '300px', overflowY: 'auto' }}>
                    {dynamicHeaders.map(h => (
                      <div key={h} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px', border: '1px solid #e2e8f0', borderRadius: '4px' }}>
                        <span style={{ fontWeight: 500 }}>{h}</span>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#ef4444' }} onClick={() => handleDeleteColumn(h)}>
                          <Trash2 size={14} />
                        </button>
                      </div>
                    ))}
                  </div>
                )}
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
                  <button className="btn btn-primary" onClick={() => setShowColumnModal(false)}>Close</button>
                </div>
              </div>
            </div>
          )}

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
      const handleSaveTemplate = async () => {
        if (!newTemplate.Name || !newTemplate.ExternalTemplateName) {
          alert('Name and External Template Name are required');
          return;
        }
        try {
          const url = isEditingTemplate ? `${API_BASE}/templates/${editingTemplateId}` : `${API_BASE}/templates`;
          const method = isEditingTemplate ? 'PUT' : 'POST';
          const res = await fetch(url, {
            method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(newTemplate)
          });
          if (res.ok) {
            setShowTemplateModal(false);
            setNewTemplate({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp' });
            setIsEditingTemplate(false);
            setEditingTemplateId(null);
            fetchData();
          } else {
            alert('Failed to save template');
          }
        } catch (err) {
          alert('Error saving template');
        }
      };

      const handleEditTemplate = (t) => {
        setNewTemplate({
          Name: t.Name || t.name,
          ExternalTemplateName: t.ExternalTemplateName || t.externalTemplateName || '',
          LanguageCode: t.LanguageCode || t.languageCode || 'en',
          ChannelType: t.ChannelType || t.channelType || 'WhatsApp'
        });
        setEditingTemplateId(t.Id || t.id);
        setIsEditingTemplate(true);
        setShowTemplateModal(true);
      };

      const handleDeleteTemplate = async (id) => {
        if (!window.confirm('Are you sure you want to delete this template?')) return;
        try {
          const res = await fetch(`${API_BASE}/templates/${id}`, { method: 'DELETE' });
          if (res.ok) {
            fetchData();
          } else {
            alert('Failed to delete template');
          }
        } catch (err) {
          alert('Error deleting template');
        }
      };

      return (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
            <h3 className="card-title">Message Templates</h3>
            <button className="btn btn-primary" onClick={() => {
              setNewTemplate({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp' });
              setIsEditingTemplate(false);
              setEditingTemplateId(null);
              setShowTemplateModal(true);
            }}>
              <Plus size={16} /> Create Template
            </button>
          </div>
          
          {showTemplateModal && (
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '400px' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>{isEditingTemplate ? 'Edit Template' : 'New Template'}</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  <input type="text" className="input" placeholder="Display Name (e.g. CheckIn Welcome)" value={newTemplate.Name} onChange={e => setNewTemplate({...newTemplate, Name: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  <input type="text" className="input" placeholder="Meta Template Name (e.g. hotel_checkin)" value={newTemplate.ExternalTemplateName} onChange={e => setNewTemplate({...newTemplate, ExternalTemplateName: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  <input type="text" className="input" placeholder="Language (e.g. en)" value={newTemplate.LanguageCode} onChange={e => setNewTemplate({...newTemplate, LanguageCode: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  <select className="input" value={newTemplate.ChannelType} onChange={e => setNewTemplate({...newTemplate, ChannelType: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}>
                    <option value="WhatsApp">WhatsApp</option>
                    <option value="SMS">SMS</option>
                  </select>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '16px' }}>
                    <button className="btn btn-secondary" onClick={() => setShowTemplateModal(false)}>Cancel</button>
                    <button className="btn btn-primary" onClick={handleSaveTemplate}>Save</button>
                  </div>
                </div>
              </div>
            </div>
          )}

          <div className="data-table-container">
            <table className="data-table">
              <thead><tr><th>Name</th><th>External Name</th><th>Channel</th><th>Actions</th></tr></thead>
              <tbody>
                {templatesList.map(t => (
                  <tr key={t.Id || t.id}>
                    <td>{t.Name || t.name}</td>
                    <td>{t.ExternalTemplateName || t.externalTemplateName || '-'}</td>
                    <td><span className="badge badge-sent">{t.ChannelType || t.channelType}</span></td>
                    <td>
                      <div style={{ display: 'flex', gap: '8px' }}>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => handleEditTemplate(t)}>
                          <Edit2 size={14} />
                        </button>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#ef4444' }} onClick={() => handleDeleteTemplate(t.Id || t.id)}>
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
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

    if (activeTab === 'broadcasts') {
      const handleSendBroadcast = async () => {
        if (!selectedBroadcastTemplate) { alert("Please select a template"); return; }
        try {
          const res = await fetch(`${API_BASE}/broadcast`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              TemplateId: parseInt(selectedBroadcastTemplate),
              CategoryId: selectedBroadcastCategory ? parseInt(selectedBroadcastCategory) : null
            })
          });
          const data = await res.json();
          alert(data.message);
          fetchData();
        } catch (err) {
          alert("Failed to queue broadcast");
        }
      };

      return (
        <div>
          <div className="card" style={{ marginBottom: '24px' }}>
            <h3 className="card-title">New Broadcast Campaign</h3>
            <div style={{ display: 'flex', gap: '16px', alignItems: 'flex-end' }}>
              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Target Audience</label>
                <select 
                  className="input" 
                  value={selectedBroadcastCategory}
                  onChange={e => setSelectedBroadcastCategory(e.target.value)}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid #e2e8f0' }}
                >
                  <option value="">All Contacts</option>
                  {categories.map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
                </select>
              </div>
              <div style={{ flex: 1 }}>
                <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Message Template</label>
                <select 
                  className="input"
                  value={selectedBroadcastTemplate}
                  onChange={e => setSelectedBroadcastTemplate(e.target.value)}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid #e2e8f0' }}
                >
                  <option value="">Select a template...</option>
                  {templatesList.map(t => <option key={t.Id || t.id} value={t.Id || t.id}>{t.Name || t.name}</option>)}
                </select>
              </div>
              <div>
                <button className="btn btn-primary" onClick={handleSendBroadcast} style={{ padding: '10px 24px' }}>
                  <Megaphone size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
                  Send Broadcast
                </button>
              </div>
            </div>
          </div>

          <div className="card">
            <h3 className="card-title">Recent Broadcasts</h3>
            <div className="data-table-container">
              <table className="data-table">
                <thead><tr><th>Started At</th><th>Template</th><th>Total Queued</th><th>Delivered</th><th>Pending</th></tr></thead>
                <tbody>
                  {broadcasts.map((b, i) => (
                    <tr key={i}>
                      <td>{new Date(b.StartedAt || b.startedAt).toLocaleString()}</td>
                      <td>{b.TemplateName || b.templateName}</td>
                      <td>{b.TotalSent || b.totalSent}</td>
                      <td><span className="badge badge-delivered">{b.Delivered || b.delivered}</span></td>
                      <td><span className="badge badge-pending">{b.Pending || b.pending}</span></td>
                    </tr>
                  ))}
                  {broadcasts.length === 0 && <tr><td colSpan="5" style={{ textAlign: 'center', padding: '24px' }}>No broadcasts sent yet.</td></tr>}
                </tbody>
              </table>
            </div>
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
          <div className={`nav-item ${activeTab === 'broadcasts' ? 'active' : ''}`} onClick={() => setActiveTab('broadcasts')}>
            <Megaphone size={20} /> Broadcasts
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
