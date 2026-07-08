import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import Papa from 'papaparse';
import { LayoutDashboard, MessagesSquare, Workflow, ScrollText, Send, Hotel, CheckCircle2, AlertCircle, Clock, Users, Upload, Plus, Megaphone, Edit2, Trash2, Settings, XCircle, BarChart2 } from 'lucide-react';
import AutomationBuilder from './components/AutomationBuilder';

const API_BASE = '/api';

export default function App() {
  const [activeTab, setActiveTab] = useState(() => localStorage.getItem('hotelAuto_activeTab') || 'dashboard');
  
  useEffect(() => {
    localStorage.setItem('hotelAuto_activeTab', activeTab);
  }, [activeTab]);
  const [stats, setStats] = useState({ templates: 0, rules: 0, outbox: 0, inbox: 0 });
  const [outbox, setOutbox] = useState([]);
  const [inbox, setInbox] = useState([]);
  const [categories, setCategories] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [totalContacts, setTotalContacts] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [dynamicHeaders, setDynamicHeaders] = useState([]);
  const [showEditContactModal, setShowEditContactModal] = useState(false);
  const [editingContactId, setEditingContactId] = useState(null);
  const [editContactData, setEditContactData] = useState({ PhoneNumber: '', FirstName: '', LastName: '', CustomFieldsJson: '{}' });
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
  
  // Interactive CSV Import States
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [previewData, setPreviewData] = useState([]);
  const [previewHeaders, setPreviewHeaders] = useState([]);
  const [importTotalRows, setImportTotalRows] = useState(0);
  const [importProgress, setImportProgress] = useState(0);
  const [analyticsRule, setAnalyticsRule] = useState(null);
  const [ruleFailures, setRuleFailures] = useState([]);
  const [parsedContacts, setParsedContacts] = useState([]);

  const [isEditingTemplate, setIsEditingTemplate] = useState(false);
  const [editingTemplateId, setEditingTemplateId] = useState(null);
  const [newTemplate, setNewTemplate] = useState({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp', BodyParamCount: -1 });
  const [isBuildingRule, setIsBuildingRule] = useState(false);
  const [editingRule, setEditingRule] = useState(null);

  useEffect(() => {
    fetchData();
    const interval = setInterval(() => {
      fetchData();
    }, 5000);
    return () => clearInterval(interval);
  }, [activeTab, currentPage, pageSize, selectedCategory]);

  const fetchData = async () => {
    try {
      const outboxRes = await fetch(`${API_BASE}/outbox`).catch(() => null);
      if (outboxRes && outboxRes.ok) {
        const outboxData = await outboxRes.json();
        setOutbox(outboxData);
        setStats(s => ({ ...s, outbox: outboxData.length }));
      }
      
      if (activeTab === 'templates' || activeTab === 'broadcasts' || activeTab === 'analytics') {
        const templatesRes = await fetch(`${API_BASE}/templates`).catch(() => null);
        if (templatesRes && templatesRes.ok) setTemplatesList(await templatesRes.json());
      }
      
      const rulesRes = await fetch(`${API_BASE}/rules`).catch(() => null);
      if (rulesRes && rulesRes.ok) {
        const rulesData = await rulesRes.json();
        // Fetch stats for each rule
        const rulesWithStats = await Promise.all(rulesData.map(async r => {
          try {
            const statsRes = await fetch(`${API_BASE}/rules/${r.Id || r.id}/stats`);
            if (statsRes.ok) {
              const stats = await statsRes.json();
              return { ...r, stats };
            }
          } catch (e) {}
          return r;
        }));
        setRulesList(rulesWithStats);
        setStats(s => ({ ...s, rules: rulesData.length }));
      }
      
      if (activeTab === 'logs') {
        const logsRes = await fetch(`${API_BASE}/logs`).catch(() => null);
        if (logsRes && logsRes.ok) setLogs(await logsRes.json());
      }

      if (activeTab === 'broadcasts' || activeTab === 'analytics') {
        const bRes = await fetch(`${API_BASE}/broadcast`).catch(() => null);
        if (bRes && bRes.ok) setBroadcasts(await bRes.json());
      }
      
      const inboxRes = await fetch(`${API_BASE}/inbox`).catch(() => null);
      if (inboxRes && inboxRes.ok) {
        const inboxData = await inboxRes.json();
        setInbox(inboxData);
        setStats(s => ({ ...s, inbox: inboxData.length }));
      }

      const catRes = await fetch(`${API_BASE}/categories`).catch(() => null);
      if (catRes && catRes.ok) setCategories(await catRes.json());
      
      const conUrl = selectedCategory ? `${API_BASE}/contacts?categoryId=${selectedCategory}&page=${currentPage}&limit=${pageSize}` : `${API_BASE}/contacts?page=${currentPage}&limit=${pageSize}`;
      const conRes = await fetch(conUrl).catch(() => null);
      if (conRes && conRes.ok) {
        const data = await conRes.json();
        setContacts(data.Contacts || data.contacts || []);
        setTotalContacts(data.TotalCount || data.totalCount || 0);
      }
      
      const colRes = await fetch(`${API_BASE}/contacts/columns/all`).catch(() => null);
      if (colRes && colRes.ok) {
        setDynamicHeaders(await colRes.json());
      }
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
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{typeof stats.outbox === 'object' ? 0 : stats.outbox}</div>
            </div>
            <div className="card" style={{ flex: 1 }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px' }}>Active Rules</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{typeof stats.rules === 'object' ? 0 : stats.rules}</div>
            </div>
            <div className="card" style={{ flex: 1 }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px' }}>Inbox Replies</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{typeof stats.inbox === 'object' ? 0 : stats.inbox}</div>
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
                      {(Array.isArray(outbox) ? outbox : []).map(msg => (
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
                      {(Array.isArray(inbox) ? inbox : []).map(msg => (
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
      const handleFileSelect = (e) => {
        const file = e.target.files[0];
        if (!file) return;
        
        setIsImporting(true); // show loading state while parsing
        Papa.parse(file, {
          header: true,
          skipEmptyLines: true,
          complete: function(results) {
            const data = results.data;
            if (data.length === 0) {
              alert("CSV is empty");
              setIsImporting(false);
              return;
            }
            
            const headers = Object.keys(data[0]);
            setPreviewHeaders(headers);
            setPreviewData(data.slice(0, 100)); // Show first 100 for preview
            
            const contactsList = data.map(row => {
                let phone = null;
                let firstName = null;
                let lastName = null;
                let customFields = {};
                
                Object.keys(row).forEach(h => {
                    const hLower = h.toLowerCase();
                    const val = row[h];
                    if (!val || val.trim() === '') return;
                    
                    const digitsCount = (hLower.match(/\d/g) || []).length;
                    const hasLetters = /[a-z]/i.test(hLower);
                    const looksLikePhoneHeader = hLower.includes('phone') || hLower.includes('mobile') || hLower.includes('whatsapp') || hLower.includes('contact') || hLower.includes('cell') || hLower.includes('number');
                    const isMissingHeaderPhone = (!hasLetters && digitsCount >= 7 && digitsCount <= 15 && (hLower.includes('+') || hLower.startsWith('0')));
                    
                    if (looksLikePhoneHeader || isMissingHeaderPhone) phone = val;
                    else if (hLower.includes('first') && hLower.includes('name')) firstName = val;
                    else if (hLower.includes('last') && hLower.includes('name')) lastName = val;
                    else customFields[h] = val;
                    
                    if (!phone) {
                        const valDigits = (val.match(/\d/g) || []).length;
                        if (!/[a-z]/i.test(val) && valDigits >= 7 && valDigits <= 15 && (val.includes('+') || val.startsWith('0'))) phone = val;
                    }
                });
                
                return {
                    PhoneNumber: phone,
                    FirstName: firstName,
                    LastName: lastName,
                    CustomFieldsJson: JSON.stringify(customFields)
                };
            }).filter(c => c.PhoneNumber);
            
            setParsedContacts(contactsList);
            setImportTotalRows(contactsList.length);
            setImportProgress(0);
            
            setIsImporting(false);
            setImportModalOpen(true);
            e.target.value = null;
          }
        });
      };

      const handleStartImport = async () => {
        setIsImporting(true);
        const chunkSize = 5000;
        let imported = 0;
        
        for (let i = 0; i < parsedContacts.length; i += chunkSize) {
            const chunk = parsedContacts.slice(i, i + chunkSize).map(c => ({
                ...c,
                CategoryId: selectedCategory || null
            }));
            
            try {
                const res = await fetch(`${API_BASE}/contacts/import/chunk`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(chunk)
                });
                if (res.ok) {
                    imported += chunk.length;
                    setImportProgress(Math.min(imported, importTotalRows));
                } else {
                    throw new Error("Failed chunk");
                }
            } catch(e) {
                alert("Import failed at chunk " + (Math.floor(i / chunkSize) + 1));
                break;
            }
        }
        
        alert(`Finished importing! Total imported: ${imported}`);
        setIsImporting(false);
        setImportModalOpen(false);
        setParsedContacts([]);
        fetchData();
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

      const handleDeleteAllContacts = async () => {
        if (!window.confirm("WARNING: Are you sure you want to delete ALL contacts? This cannot be undone!")) return;
        try {
          const res = await fetch(`${API_BASE}/contacts`, { method: 'DELETE' });
          if (res.ok) {
            alert("All contacts deleted.");
            setCurrentPage(1);
            fetchData();
          }
        } catch (e) {
          alert("Error deleting contacts.");
        }
      };

      const handleDeleteContact = async (id) => {
        if (!window.confirm("Delete this contact?")) return;
        try {
          const res = await fetch(`${API_BASE}/contacts/${id}`, { method: 'DELETE' });
          if (res.ok) fetchData();
        } catch(e) {
          alert("Error deleting contact.");
        }
      };

      const handleEditContactClick = (c) => {
        const defaultFields = {};
        (Array.isArray(dynamicHeaders) ? dynamicHeaders : []).forEach(h => defaultFields[h] = '');
        let existing = {};
        try { existing = JSON.parse(c.CustomFieldsJson || c.customFieldsJson || '{}'); } catch(e){}
        const mergedFields = { ...defaultFields, ...existing };

        setEditingContactId(c.Id || c.id);
        setEditContactData({
            PhoneNumber: c.PhoneNumber || c.phoneNumber || '',
            FirstName: c.FirstName || c.firstName || '',
            LastName: c.LastName || c.lastName || '',
            CustomFieldsJson: JSON.stringify(mergedFields)
        });
        setShowEditContactModal(true);
      };

      const handleAddContactClick = () => {
        const defaultFields = {};
        (Array.isArray(dynamicHeaders) ? dynamicHeaders : []).forEach(h => defaultFields[h] = '');

        setEditingContactId(null);
        setEditContactData({
            PhoneNumber: '',
            FirstName: '',
            LastName: '',
            CustomFieldsJson: JSON.stringify(defaultFields)
        });
        setShowEditContactModal(true);
      };

      const handleSaveContact = async () => {
          try {
              const url = editingContactId ? `${API_BASE}/contacts/${editingContactId}` : `${API_BASE}/contacts`;
              const method = editingContactId ? 'PUT' : 'POST';
              
              const res = await fetch(url, {
                  method,
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({
                      ...editContactData,
                      CategoryId: selectedCategory || null
                  })
              });
              if (res.ok) {
                  setShowEditContactModal(false);
                  fetchData();
              } else {
                  alert("Failed to save contact.");
              }
          } catch(e) {
              alert("Error saving contact.");
          }
      };

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
              onChange={e => { setSelectedCategory(e.target.value); setCurrentPage(1); }} 
              style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
            >
              <option value="">All Categories</option>
              {(Array.isArray(categories) ? categories : []).map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
            </select>
            <button className="btn btn-secondary" onClick={handleAddCategory}>
              <Plus size={16} /> New Category
            </button>
            <div style={{ flex: 1 }}></div>
            <button className="btn btn-secondary" onClick={() => setShowColumnModal(true)}>
              <Settings size={16} /> Manage Columns
            </button>
            <button className="btn btn-secondary" onClick={handleAddContactClick}>
              <Plus size={16} /> Add Contact
            </button>
            <button className="btn btn-secondary" onClick={handleDeleteAllContacts} style={{ color: '#ef4444', borderColor: '#ef4444' }}>
              <Trash2 size={16} /> Delete All
            </button>
            <label className="btn btn-primary" style={{ cursor: isImporting ? 'not-allowed' : 'pointer', backgroundColor: isImporting ? '#94a3b8' : '#10b981' }}>
              {isImporting ? 'Loading CSV...' : <><Upload size={16} /> Import CSV</>}
              <input type="file" accept=".csv" style={{ display: 'none' }} onChange={handleFileSelect} disabled={isImporting} />
            </label>
          </div>

          {showColumnModal && createPortal((
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
                    {(Array.isArray(dynamicHeaders) ? dynamicHeaders : []).map(h => (
                      <div key={h} style={{ display: 'flex', justifyContent: 'space-between', padding: '8px', borderBottom: '1px solid #eee', borderRadius: '4px' }}>
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
          ), document.body)}

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
                      {(Array.isArray(dynamicHeaders) ? dynamicHeaders : []).map(h => <th key={h}>{h}</th>)}
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(Array.isArray(contacts) ? contacts : []).map(c => {
                      let fields = {};
                      try { fields = JSON.parse(c.CustomFieldsJson || c.customFieldsJson || '{}'); } catch(e){}
                      return (
                        <tr key={c.Id || c.id}>
                          <td style={{ fontFamily: 'monospace' }}>{c.PhoneNumber || c.phoneNumber}</td>
                          <td>{c.FirstName || c.firstName}</td>
                          <td>{c.LastName || c.lastName}</td>
                          {(Array.isArray(dynamicHeaders) ? dynamicHeaders : []).map(h => <td key={h}>{fields[h] || '-'}</td>)}
                          <td>
                            <div style={{ display: 'flex', gap: '8px' }}>
                              <button className="btn btn-secondary" style={{ padding: '4px', border: 'none' }} onClick={() => handleEditContactClick(c)}>
                                <Edit2 size={16} color="#3b82f6" />
                              </button>
                              <button className="btn btn-secondary" style={{ padding: '4px', border: 'none' }} onClick={() => handleDeleteContact(c.Id || c.id)}>
                                <Trash2 size={16} color="#ef4444" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
            {contacts.length > 0 && (
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', padding: '16px', borderTop: '1px solid #e2e8f0' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <span style={{ color: '#64748b' }}>Rows per page:</span>
                  <select 
                    value={pageSize} 
                    onChange={(e) => { setPageSize(Number(e.target.value)); setCurrentPage(1); }}
                    style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value={10}>10</option>
                    <option value={20}>20</option>
                    <option value={50}>50</option>
                    <option value={100}>100</option>
                  </select>
                  <span style={{ color: '#64748b', marginLeft: '16px' }}>Total: {totalContacts}</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                  <button 
                    className="btn btn-secondary" 
                    disabled={currentPage === 1} 
                    onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                  >
                    Previous
                  </button>
                  <span style={{ fontWeight: 500 }}>Page {currentPage} of {Math.ceil(totalContacts / pageSize) || 1}</span>
                  <button 
                    className="btn btn-secondary" 
                    disabled={currentPage >= Math.ceil(totalContacts / pageSize)} 
                    onClick={() => setCurrentPage(p => p + 1)}
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </div>
          
          {showEditContactModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '500px' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>{editingContactId ? 'Edit Contact' : 'Add Contact'}</h3>
                
                <div className="form-group" style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Phone Number</label>
                  <input type="text" value={editContactData.PhoneNumber} onChange={e => setEditContactData({...editContactData, PhoneNumber: e.target.value})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                </div>
                
                <div style={{ display: 'flex', gap: '16px', marginBottom: '16px' }}>
                  <div className="form-group" style={{ flex: 1 }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>First Name</label>
                    <input type="text" value={editContactData.FirstName} onChange={e => setEditContactData({...editContactData, FirstName: e.target.value})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  </div>
                  <div className="form-group" style={{ flex: 1 }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Last Name</label>
                    <input type="text" value={editContactData.LastName} onChange={e => setEditContactData({...editContactData, LastName: e.target.value})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  </div>
                </div>

                <div className="form-group" style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Custom Fields</label>
                  {(() => {
                    let fields = {};
                    try { fields = JSON.parse(editContactData.CustomFieldsJson || '{}'); } catch(e) {}
                    const keys = Object.keys(fields);
                    
                    return (
                      <div>
                        {keys.map((k, i) => (
                          <div key={i} style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                            <input 
                              type="text" 
                              value={k} 
                              onChange={e => {
                                const newKey = e.target.value;
                                const newFields = {};
                                // preserve order
                                keys.forEach(oldKey => {
                                  if (oldKey === k) newFields[newKey] = fields[oldKey];
                                  else newFields[oldKey] = fields[oldKey];
                                });
                                setEditContactData({...editContactData, CustomFieldsJson: JSON.stringify(newFields)});
                              }}
                              style={{ width: '40%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                            />
                            <input 
                              type="text" 
                              value={fields[k]} 
                              onChange={e => {
                                const newFields = { ...fields, [k]: e.target.value };
                                setEditContactData({...editContactData, CustomFieldsJson: JSON.stringify(newFields)});
                              }}
                              style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                            />
                            <button 
                              className="btn btn-secondary"
                              style={{ color: '#ef4444', padding: '8px' }}
                              onClick={() => {
                                const newFields = { ...fields };
                                delete newFields[k];
                                setEditContactData({...editContactData, CustomFieldsJson: JSON.stringify(newFields)});
                              }}
                            >
                              <XCircle size={16} />
                            </button>
                          </div>
                        ))}
                        <button 
                          className="btn btn-secondary" 
                          style={{ marginTop: '8px', padding: '4px 8px', fontSize: '0.9em' }}
                          onClick={() => {
                            let newKey = 'new_field';
                            let counter = 1;
                            while(fields[newKey]) {
                                newKey = 'new_field_' + counter;
                                counter++;
                            }
                            const newFields = { ...fields, [newKey]: '' };
                            setEditContactData({...editContactData, CustomFieldsJson: JSON.stringify(newFields)});
                          }}
                        >
                          <Plus size={14} style={{ marginRight: '4px', verticalAlign: 'middle' }} /> Add Field
                        </button>
                      </div>
                    );
                  })()}
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                  <button className="btn btn-secondary" onClick={() => setShowEditContactModal(false)}>Cancel</button>
                  <button className="btn btn-primary" onClick={handleSaveContact}>Save</button>
                </div>
              </div>
            </div>
          ), document.body)}

          {importModalOpen && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '800px', maxHeight: '90vh', overflowY: 'auto' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>Import Contacts Preview</h3>
                
                <div style={{ display: 'flex', gap: '16px', marginBottom: '24px', alignItems: 'center' }}>
                  <div style={{ flex: 1 }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Select Category for these contacts</label>
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <select 
                        value={selectedCategory} 
                        onChange={e => setSelectedCategory(e.target.value)} 
                        style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc', flex: 1 }}
                      >
                        <option value="">No Category (All Contacts)</option>
                        {(Array.isArray(categories) ? categories : []).map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
                      </select>
                      <button className="btn btn-secondary" onClick={handleAddCategory}>
                        <Plus size={16} /> Create New
                      </button>
                    </div>
                  </div>
                </div>

                <div style={{ marginBottom: '16px' }}>
                  <p style={{ fontWeight: 500 }}>Total Valid Contacts Detected: {importTotalRows}</p>
                  <p style={{ color: '#64748b', fontSize: '0.9em' }}>Previewing first 100 rows:</p>
                </div>

                <div className="data-table-container" style={{ maxHeight: '300px', marginBottom: '24px' }}>
                  <table className="data-table">
                    <thead>
                      <tr>
                        {previewHeaders.map(h => <th key={h}>{h}</th>)}
                      </tr>
                    </thead>
                    <tbody>
                      {previewData.map((row, i) => (
                        <tr key={i}>
                          {previewHeaders.map(h => <td key={h}>{row[h]}</td>)}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {isImporting && (
                  <div style={{ marginBottom: '24px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                      <span style={{ fontWeight: 500 }}>Importing Contacts...</span>
                      <span>{importProgress} / {importTotalRows}</span>
                    </div>
                    <div style={{ width: '100%', height: '8px', backgroundColor: '#e2e8f0', borderRadius: '4px', overflow: 'hidden' }}>
                      <div style={{ width: `${(importProgress / importTotalRows) * 100}%`, height: '100%', backgroundColor: '#10b981', transition: 'width 0.3s ease' }}></div>
                    </div>
                  </div>
                )}

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                  <button className="btn btn-secondary" onClick={() => setImportModalOpen(false)} disabled={isImporting}>Cancel</button>
                  <button className="btn btn-primary" onClick={handleStartImport} disabled={isImporting || importTotalRows === 0}>
                    {isImporting ? 'Processing...' : 'Start Import'}
                  </button>
                </div>
              </div>
            </div>
          ), document.body)}
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
            setNewTemplate({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp', BodyParamCount: -1 });
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
          ChannelType: t.ChannelType || t.channelType || 'WhatsApp',
          BodyParamCount: t.BodyParamCount ?? t.bodyParamCount ?? -1
        });
        setEditingTemplateId(t.Id || t.id);
        setIsEditingTemplate(true);
        setShowTemplateModal(true);
      };

      const loadRules = async () => {
        try {
          const res = await fetch(`${API_BASE}/rules`);
          if (res.ok) {
            const rules = await res.json();
            // For each rule, fetch stats
            const rulesWithStats = await Promise.all(rules.map(async r => {
              try {
                const statsRes = await fetch(`${API_BASE}/rules/${r.Id || r.id}/stats`);
                if (statsRes.ok) {
                  const stats = await statsRes.json();
                  return { ...r, stats };
                }
              } catch (e) {}
              return r;
            }));
            setRulesList(rulesWithStats);
          }
        } catch (e) {
          console.error(e);
        }
      };

      const handleDeleteTemplate = async (id) => {
        if (!window.confirm('Are you sure you want to delete this template?')) return;
        try {
          const res = await fetch(`${API_BASE}/templates/${id}`, { method: 'DELETE' });
          if (res.ok) {
            fetchData();
          } else {
            let errorMsg = 'Failed to delete template';
            try {
              const data = await res.json();
              if (data && data.message) errorMsg = data.message;
            } catch (e) { /* ignore parse error */ }
            alert(errorMsg);
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
              setNewTemplate({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp', BodyParamCount: -1 });
              setIsEditingTemplate(false);
              setEditingTemplateId(null);
              setShowTemplateModal(true);
            }}>
              <Plus size={16} /> Create Template
            </button>
          </div>
          
          {showTemplateModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '400px' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>{isEditingTemplate ? 'Edit Template' : 'New Template'}</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  <input type="text" className="input" placeholder="Display Name (e.g. CheckIn Welcome)" value={newTemplate.Name} onChange={e => setNewTemplate({...newTemplate, Name: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  <input type="text" className="input" placeholder="Meta Template Name (e.g. hotel_checkin)" value={newTemplate.ExternalTemplateName} onChange={e => setNewTemplate({...newTemplate, ExternalTemplateName: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  <input type="text" className="input" placeholder="Language (e.g. en_US)" value={newTemplate.LanguageCode} onChange={e => setNewTemplate({...newTemplate, LanguageCode: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  <select className="input" value={newTemplate.ChannelType} onChange={e => setNewTemplate({...newTemplate, ChannelType: e.target.value})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}>
                    <option value="WhatsApp">WhatsApp</option>
                    <option value="SMS">SMS</option>
                  </select>
                  <div>
                    <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Body Parameters</label>
                    <select className="input" value={newTemplate.BodyParamCount} onChange={e => setNewTemplate({...newTemplate, BodyParamCount: parseInt(e.target.value)})} style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}>
                      <option value={-1}>Unknown / Has Parameters (default)</option>
                      <option value={0}>Zero Parameters (e.g. hello_world)</option>
                    </select>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '16px' }}>
                    <button className="btn btn-secondary" onClick={() => setShowTemplateModal(false)}>Cancel</button>
                    <button className="btn btn-primary" onClick={handleSaveTemplate}>Save</button>
                  </div>
                </div>
              </div>
            </div>
          ), document.body)}

          <div className="data-table-container">
            <table className="data-table">
              <thead><tr><th>Name</th><th>External Name</th><th>Channel</th><th>Actions</th></tr></thead>
              <tbody>
                {(Array.isArray(templatesList) ? templatesList : []).map(t => (
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
      const handleEditRule = (rule) => {
        setEditingRule(rule);
        setIsBuildingRule(true);
      };

      const handleShowAnalytics = async (rule) => {
        setAnalyticsRule(rule);
        setRuleFailures([]);
        try {
          const res = await fetch(`${API_BASE}/rules/${rule.Id || rule.id}/failures`);
          if (res.ok) {
            setRuleFailures(await res.json());
          }
        } catch (e) {
          console.error("Failed to fetch failures", e);
        }
      };

      const handleDeleteRule = async (id) => {
        if (!window.confirm("Are you sure you want to delete this rule?")) return;
        try {
          const res = await fetch(`${API_BASE}/rules/${id}`, { method: 'DELETE' });
          if (res.ok) {
            setRulesList(prev => prev.filter(r => (r.Id || r.id) !== id));
          } else {
            alert("Failed to delete rule");
          }
        } catch(e) {
          alert("Error deleting rule");
        }
      };

      const handleToggleRule = async (id, currentState) => {
        try {
          const res = await fetch(`${API_BASE}/rules/${id}/toggle`, { method: 'PUT' });
          if (res.ok) {
            setRulesList(prev => prev.map(r => (r.Id || r.id) === id ? { ...r, IsEnabled: !currentState, isEnabled: !currentState } : r));
          } else {
            alert("Failed to toggle rule");
          }
        } catch(e) {
          alert("Error toggling rule");
        }
      };

      if (isBuildingRule) {
        return <AutomationBuilder onCancel={() => { setIsBuildingRule(false); setEditingRule(null); }} initialRule={editingRule} />;
      }
      return (
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
            <h3 className="card-title">Automation Rules</h3>
            <button className="btn btn-primary" onClick={() => { setEditingRule(null); setIsBuildingRule(true); }}>
              <Plus size={16} /> Create Rule
            </button>
          </div>
          <div className="data-table-container">
            <table className="data-table">
              <thead><tr><th>Name</th><th>Trigger</th><th>Action Template ID</th><th>Stats</th><th>Status</th><th>Actions</th></tr></thead>
              <tbody>
                {(Array.isArray(rulesList) ? rulesList : []).map(r => (
                  <tr key={r.Id || r.id} style={{ opacity: (r.IsEnabled ?? r.isEnabled) ? 1 : 0.6 }}>
                    <td>
                      <div style={{ fontWeight: 600 }}>{r.Name || r.name}</div>
                      <div style={{ fontSize: '0.8rem', color: '#64748b' }}>{r.Description || r.description}</div>
                    </td>
                    <td><span className="badge badge-pending">{r.TriggerEventType || r.triggerEventType}</span></td>
                    <td><span className="badge badge-delivered">Template: {r.TemplateId || r.templateId}</span></td>
                    <td style={{ fontSize: '0.8rem' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                        <div style={{ fontWeight: 600, color: '#475569', marginBottom: '2px' }}>Analytics</div>
                        <div style={{ display: 'flex', gap: '8px' }}>
                          <span title="Triggered" style={{ color: '#3b82f6' }}>T: {r.stats?.Triggered ?? r.stats?.triggered ?? 0}</span>
                          <span title="Sent" style={{ color: '#10b981' }}>S: {r.stats?.TotalSent ?? r.stats?.totalSent ?? 0}</span>
                          <span title="Failed" style={{ color: '#ef4444' }}>F: {r.stats?.Failed ?? r.stats?.failed ?? 0}</span>
                        </div>
                        <div style={{ fontSize: '0.7rem', color: '#94a3b8' }}>
                          {r.stats?.LastRun || r.stats?.lastRun ? `Last: ${new Date(r.stats?.LastRun || r.stats?.lastRun).toLocaleString()}` : 'Never run'}
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className={`badge ${(r.IsEnabled ?? r.isEnabled) ? 'badge-delivered' : 'badge-failed'}`}>
                        {(r.IsEnabled ?? r.isEnabled) ? 'Active' : 'Paused'}
                      </span>
                    </td>
                     <td>
                      <div style={{ display: 'flex', gap: '8px' }}>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => handleEditRule(r)} title="Edit">
                          <Edit2 size={14} />
                        </button>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: (r.IsEnabled ?? r.isEnabled) ? '#f59e0b' : '#10b981' }} onClick={() => handleToggleRule(r.Id || r.id, (r.IsEnabled ?? r.isEnabled))} title={(r.IsEnabled ?? r.isEnabled) ? "Pause" : "Resume"}>
                          {(r.IsEnabled ?? r.isEnabled) ? <XCircle size={14} /> : <CheckCircle2 size={14} />}
                        </button>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#3b82f6' }} onClick={() => handleShowAnalytics(r)} title="Analytics">
                          <BarChart2 size={14} />
                        </button>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#ef4444' }} onClick={() => handleDeleteRule(r.Id || r.id)} title="Delete">
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {analyticsRule && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '600px', maxWidth: '95%', maxHeight: '90vh', display: 'flex', flexDirection: 'column' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #e2e8f0', paddingBottom: '12px' }}>
                  <h3 className="card-title" style={{ margin: 0 }}>Analytics: {analyticsRule.Name || analyticsRule.name}</h3>
                  <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => setAnalyticsRule(null)}>✕</button>
                </div>
                
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '24px' }}>
                  <div style={{ padding: '16px', backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.8rem', color: '#166534', fontWeight: 600 }}>TRIGGERED</div>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#15803d' }}>
                      {analyticsRule.stats?.Triggered ?? analyticsRule.stats?.triggered ?? 0}
                    </div>
                  </div>
                  <div style={{ padding: '16px', backgroundColor: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '8px', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.8rem', color: '#1e40af', fontWeight: 600 }}>SENT SUCCESSFULLY</div>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#1d4ed8' }}>
                      {analyticsRule.stats?.TotalSent ?? analyticsRule.stats?.totalSent ?? 0}
                    </div>
                  </div>
                  <div style={{ padding: '16px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.8rem', color: '#991b1b', fontWeight: 600 }}>FAILED</div>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#b91c1c' }}>
                      {analyticsRule.stats?.Failed ?? analyticsRule.stats?.failed ?? 0}
                    </div>
                  </div>
                </div>

                <h4 style={{ marginBottom: '8px', color: '#334155', fontWeight: 600 }}>Failure Log (Recent Failed Numbers & Reasons)</h4>
                <div style={{ flex: 1, overflowY: 'auto', maxHeight: '350px', border: '1px solid #e2e8f0', borderRadius: '6px', backgroundColor: '#f8fafc', padding: '12px' }}>
                  {ruleFailures.length === 0 ? (
                    <div style={{ textAlign: 'center', color: '#64748b', padding: '24px 0' }}>
                      No failed messages recorded for this rule.
                    </div>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      {ruleFailures.map((f, i) => (
                        <div key={i} style={{ padding: '10px', backgroundColor: '#ffffff', border: '1px solid #e2e8f0', borderRadius: '6px', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                            <span style={{ fontWeight: 600, color: '#334155' }}>Phone: {f.ToPhoneNumber || f.toPhoneNumber}</span>
                            <span style={{ color: '#64748b' }}>{new Date(f.CreatedAt || f.createdAt).toLocaleString()}</span>
                          </div>
                          <div style={{ fontSize: '0.8rem', color: '#ef4444', backgroundColor: '#fef2f2', padding: '6px 8px', borderRadius: '4px', border: '1px solid #fee2e2' }}>
                            <strong>Reason:</strong> {f.FailureReason || f.failureReason || 'Failed to connect to Meta WhatsApp API.'}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px', paddingTop: '12px', borderTop: '1px solid #e2e8f0' }}>
                  <button className="btn btn-primary" onClick={() => setAnalyticsRule(null)}>Close</button>
                </div>
              </div>
            </div>
          ), document.body)}
        </div>
      );
    }

    if (activeTab === 'analytics') {
      const handleShowAnalytics = async (rule) => {
        setAnalyticsRule(rule);
        setRuleFailures([]);
        try {
          const res = await fetch(`${API_BASE}/rules/${rule.Id || rule.id}/failures`);
          if (res.ok) {
            setRuleFailures(await res.json());
          }
        } catch (e) {
          console.error("Failed to fetch failures", e);
        }
      };

      // Aggregate overall stats
      const totalAutomationsTriggered = (rulesList || []).reduce((acc, r) => acc + (r.stats?.Triggered ?? r.stats?.triggered ?? 0), 0);
      const totalAutomationsSent = (rulesList || []).reduce((acc, r) => acc + (r.stats?.TotalSent ?? r.stats?.totalSent ?? 0), 0);
      const totalAutomationsFailed = (rulesList || []).reduce((acc, r) => acc + (r.stats?.Failed ?? r.stats?.failed ?? 0), 0);

      const totalBroadcastsQueued = (broadcasts || []).reduce((acc, b) => acc + (b.TotalQueued ?? b.totalQueued ?? b.TotalSent ?? b.totalSent ?? 0), 0);
      const totalBroadcastsSent = (broadcasts || []).reduce((acc, b) => acc + (b.Delivered ?? b.delivered ?? 0), 0);
      const totalBroadcastsFailed = (broadcasts || []).reduce((acc, b) => acc + (b.Failed ?? b.failed ?? 0), 0);

      const overallTriggered = totalAutomationsTriggered + totalBroadcastsQueued;
      const overallSent = totalAutomationsSent + totalBroadcastsSent;
      const overallFailed = totalAutomationsFailed + totalBroadcastsFailed;
      const successRate = overallTriggered > 0 ? ((overallSent / (overallSent + overallFailed || 1)) * 100).toFixed(1) : '100.0';

      return (
        <div>
          <div style={{ display: 'flex', gap: '24px', marginBottom: '32px' }}>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #3b82f6' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>OVERALL TRIGGERED</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{overallTriggered}</div>
            </div>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #10b981' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>SUCCESSFULLY SENT</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#10b981' }}>{overallSent}</div>
            </div>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #ef4444' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>FAILED DELIVERIES</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#ef4444' }}>{overallFailed}</div>
            </div>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #eab308' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>DELIVERY SUCCESS RATE</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#eab308' }}>{successRate}%</div>
            </div>
          </div>

          <div className="card" style={{ marginBottom: '32px' }}>
            <h3 className="card-title" style={{ marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Workflow size={20} color="var(--primary)" /> Automation Rules Performance
            </h3>
            <div className="data-table-container">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Rule Name</th>
                    <th>Trigger Event</th>
                    <th style={{ textAlign: 'center' }}>Triggered</th>
                    <th style={{ textAlign: 'center' }}>Sent</th>
                    <th style={{ textAlign: 'center' }}>Failed</th>
                    <th style={{ textAlign: 'center' }}>Success Rate</th>
                    <th style={{ textAlign: 'center' }}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {(rulesList || []).map(r => {
                    const trig = r.stats?.Triggered ?? r.stats?.triggered ?? 0;
                    const s = r.stats?.TotalSent ?? r.stats?.totalSent ?? 0;
                    const f = r.stats?.Failed ?? r.stats?.failed ?? 0;
                    const ruleRate = trig > 0 ? ((s / (s + f || 1)) * 100).toFixed(1) : '100.0';
                    return (
                      <tr key={r.Id || r.id}>
                        <td style={{ fontWeight: 600 }}>{r.Name || r.name}</td>
                        <td><span className="badge badge-pending">{r.TriggerEventType || r.triggerEventType}</span></td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{trig}</td>
                        <td style={{ textAlign: 'center', color: '#10b981', fontWeight: 600 }}>{s}</td>
                        <td style={{ textAlign: 'center', color: '#ef4444', fontWeight: 600 }}>{f}</td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{ruleRate}%</td>
                        <td style={{ textAlign: 'center' }}>
                          <button 
                            className="btn btn-secondary" 
                            style={{ padding: '6px 12px', fontSize: '0.8rem', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                            onClick={() => handleShowAnalytics(r)}
                          >
                            <BarChart2 size={12} /> View Failures
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                  {(rulesList || []).length === 0 && (
                    <tr>
                      <td colSpan="7" style={{ textAlign: 'center', padding: '24px' }}>No automation rules found.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="card">
            <h3 className="card-title" style={{ marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Megaphone size={20} color="var(--primary)" /> Broadcast Campaigns Performance
            </h3>
            <div className="data-table-container">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Started At</th>
                    <th>Template Name</th>
                    <th style={{ textAlign: 'center' }}>Total Queued</th>
                    <th style={{ textAlign: 'center' }}>Sent</th>
                    <th style={{ textAlign: 'center' }}>Failed</th>
                    <th style={{ textAlign: 'center' }}>Pending</th>
                    <th style={{ textAlign: 'center' }}>Success Rate</th>
                  </tr>
                </thead>
                <tbody>
                  {(broadcasts || []).map((b, i) => {
                    const total = b.TotalQueued ?? b.totalQueued ?? b.TotalSent ?? b.totalSent ?? 0;
                    const s = b.Delivered ?? b.delivered ?? 0;
                    const f = b.Failed ?? b.failed ?? 0;
                    const p = b.Pending ?? b.pending ?? 0;
                    const rate = total > 0 ? ((s / (s + f || 1)) * 100).toFixed(1) : '100.0';
                    return (
                      <tr key={i}>
                        <td>{new Date(b.StartedAt || b.startedAt).toLocaleString()}</td>
                        <td style={{ fontWeight: 600 }}>{b.TemplateName || b.templateName}</td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{total}</td>
                        <td style={{ textAlign: 'center', color: '#10b981', fontWeight: 600 }}>{s}</td>
                        <td style={{ textAlign: 'center', color: '#ef4444', fontWeight: 600 }}>{f}</td>
                        <td style={{ textAlign: 'center', color: '#eab308', fontWeight: 600 }}>{p}</td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{rate}%</td>
                      </tr>
                    );
                  })}
                  {(broadcasts || []).length === 0 && (
                    <tr>
                      <td colSpan="7" style={{ textAlign: 'center', padding: '24px' }}>No broadcasts sent yet.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {analyticsRule && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '600px', maxWidth: '95%', maxHeight: '90vh', display: 'flex', flexDirection: 'column' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #e2e8f0', paddingBottom: '12px' }}>
                  <h3 className="card-title" style={{ margin: 0 }}>Analytics: {analyticsRule.Name || analyticsRule.name}</h3>
                  <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => setAnalyticsRule(null)}>✕</button>
                </div>
                
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '24px' }}>
                  <div style={{ padding: '16px', backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.8rem', color: '#166534', fontWeight: 600 }}>TRIGGERED</div>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#15803d' }}>
                      {analyticsRule.stats?.Triggered ?? analyticsRule.stats?.triggered ?? 0}
                    </div>
                  </div>
                  <div style={{ padding: '16px', backgroundColor: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '8px', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.8rem', color: '#1e40af', fontWeight: 600 }}>SENT SUCCESSFULLY</div>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#1d4ed8' }}>
                      {analyticsRule.stats?.TotalSent ?? analyticsRule.stats?.totalSent ?? 0}
                    </div>
                  </div>
                  <div style={{ padding: '16px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', textAlign: 'center' }}>
                    <div style={{ fontSize: '0.8rem', color: '#991b1b', fontWeight: 600 }}>FAILED</div>
                    <div style={{ fontSize: '1.8rem', fontWeight: 'bold', color: '#b91c1c' }}>
                      {analyticsRule.stats?.Failed ?? analyticsRule.stats?.failed ?? 0}
                    </div>
                  </div>
                </div>

                <h4 style={{ marginBottom: '8px', color: '#334155', fontWeight: 600 }}>Failure Log (Recent Failed Numbers & Reasons)</h4>
                <div style={{ flex: 1, overflowY: 'auto', maxHeight: '350px', border: '1px solid #e2e8f0', borderRadius: '6px', backgroundColor: '#f8fafc', padding: '12px' }}>
                  {ruleFailures.length === 0 ? (
                    <div style={{ textAlign: 'center', color: '#64748b', padding: '24px 0' }}>
                      No failed messages recorded for this rule.
                    </div>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                      {ruleFailures.map((f, i) => (
                        <div key={i} style={{ padding: '10px', backgroundColor: '#ffffff', border: '1px solid #e2e8f0', borderRadius: '6px', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85rem' }}>
                            <span style={{ fontWeight: 600, color: '#334155' }}>Phone: {f.ToPhoneNumber || f.toPhoneNumber}</span>
                            <span style={{ color: '#64748b' }}>{new Date(f.CreatedAt || f.createdAt).toLocaleString()}</span>
                          </div>
                          <div style={{ fontSize: '0.8rem', color: '#ef4444', backgroundColor: '#fef2f2', padding: '6px 8px', borderRadius: '4px', border: '1px solid #fee2e2' }}>
                            <strong>Reason:</strong> {f.FailureReason || f.failureReason || 'Failed to connect to Meta WhatsApp API.'}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px', paddingTop: '12px', borderTop: '1px solid #e2e8f0' }}>
                  <button className="btn btn-primary" onClick={() => setAnalyticsRule(null)}>Close</button>
                </div>
              </div>
            </div>
          ), document.body)}
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
                {(Array.isArray(logs) ? logs : []).map(l => (
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

      const handleStopBroadcast = async (b) => {
        if (!window.confirm('Are you sure you want to stop this broadcast? All pending messages will be cancelled.')) return;
        try {
          const res = await fetch(`${API_BASE}/broadcast?templateId=${b.TemplateId || b.templateId}&startedAt=${encodeURIComponent(b.StartedAt || b.startedAt)}`, {
            method: 'DELETE'
          });
          const data = await res.json();
          alert(data.message);
          fetchData();
        } catch (err) {
          alert("Failed to stop broadcast");
        }
      };

      const handleDeleteBroadcast = async (b) => {
        if (!window.confirm('Are you sure you want to completely delete this broadcast from history?')) return;
        try {
          const res = await fetch(`${API_BASE}/broadcast/history?templateId=${b.TemplateId || b.templateId}&startedAt=${encodeURIComponent(b.StartedAt || b.startedAt)}`, {
            method: 'DELETE'
          });
          const data = await res.json();
          alert(data.message);
          fetchData();
        } catch (err) {
          alert("Failed to delete broadcast");
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
                  {(Array.isArray(categories) ? categories : []).map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
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
                  {(Array.isArray(templatesList) ? templatesList : []).map(t => <option key={t.Id || t.id} value={t.Id || t.id}>{t.Name || t.name}</option>)}
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
                <thead><tr><th>Started At</th><th>Template</th><th>Total Queued</th><th>Delivered</th><th>Pending</th><th>Failed</th><th>Actions</th></tr></thead>
                <tbody>
                  {(Array.isArray(broadcasts) ? broadcasts : []).map((b, i) => (
                    <tr key={i}>
                      <td>{new Date(b.StartedAt || b.startedAt).toLocaleString()}</td>
                      <td>{b.TemplateName || b.templateName}</td>
                      <td>{b.TotalQueued ?? b.totalQueued ?? b.TotalSent ?? b.totalSent ?? 0}</td>
                      <td><span className="badge badge-delivered">{b.Delivered ?? b.delivered ?? 0}</span></td>
                      <td><span className="badge badge-pending">{b.Pending ?? b.pending ?? 0}</span></td>
                      <td><span className="badge" style={{ backgroundColor: '#fee2e2', color: '#991b1b', padding: '4px 8px', borderRadius: '12px', fontSize: '0.75rem', fontWeight: 600 }}>{b.Failed ?? b.failed ?? 0}</span></td>
                      <td>
                        <div style={{ display: 'flex', gap: '8px' }}>
                          {(b.Pending || b.pending) > 0 && (
                            <button 
                              className="btn btn-secondary" 
                              style={{ padding: '4px 8px', fontSize: '0.8rem', color: '#eab308', borderColor: '#eab308' }} 
                              onClick={() => handleStopBroadcast(b)}
                              title="Stop pending messages"
                            >
                              <XCircle size={14} style={{ marginRight: '4px', verticalAlign: 'middle' }} />
                              Stop
                            </button>
                          )}
                          <button 
                            className="btn btn-secondary" 
                            style={{ padding: '4px 8px', fontSize: '0.8rem', color: '#ef4444', borderColor: '#ef4444' }} 
                            onClick={() => handleDeleteBroadcast(b)}
                            title="Delete from history"
                          >
                            <Trash2 size={14} style={{ marginRight: '4px', verticalAlign: 'middle' }} />
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {(broadcasts || []).length === 0 && <tr><td colSpan="6" style={{ textAlign: 'center', padding: '24px' }}>No broadcasts sent yet.</td></tr>}
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
          <div className={`nav-item ${activeTab === 'analytics' ? 'active' : ''}`} onClick={() => setActiveTab('analytics')}>
            <BarChart2 size={20} /> Analytics
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
