import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import Papa from 'papaparse';
import { LayoutDashboard, MessagesSquare, Workflow, ScrollText, Send, Hotel, CheckCircle2, AlertCircle, Clock, Users, Upload, Plus, Megaphone, Edit2, Trash2, Settings, XCircle, BarChart2, UserX, Layers, FileText, Menu, Download } from 'lucide-react';
import AutomationBuilder from './components/AutomationBuilder';
import PreferenceCenter from './components/PreferenceCenter';
import TemplateVariantManager from './components/TemplateVariantManager';
import ManualCheckIn from './components/ManualCheckIn';
import ManualCheckOut from './components/ManualCheckOut';

const API_BASE = '/api';

export default function App() {
  const params = new URLSearchParams(window.location.search);
  const prefContactId = params.get('preferences_for');

  if (prefContactId) {
    return <PreferenceCenter contactId={parseInt(prefContactId)} />;
  }

  const [activeTab, setActiveTab] = useState(() => localStorage.getItem('hotelAuto_activeTab') || 'dashboard');
  
  useEffect(() => {
    localStorage.setItem('hotelAuto_activeTab', activeTab);
  }, [activeTab]);
  const [stats, setStats] = useState({ templates: 0, rules: 0, outbox: 0, inbox: 0 });
  const [outbox, setOutbox] = useState([]);
  const [inbox, setInbox] = useState([]);
  const [categories, setCategories] = useState([]);
  const [contacts, setContacts] = useState([]);
  const [smartLists, setSmartLists] = useState([]);
  const [selectedSmartList, setSelectedSmartList] = useState('');
  const [totalContacts, setTotalContacts] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [dynamicHeaders, setDynamicHeaders] = useState([]);
  const [showSmartListModal, setShowSmartListModal] = useState(false);
  const [newSmartList, setNewSmartList] = useState({ ListName: '', Filters: [{ field: 'City', operator: 'equals', value: '' }] });
  const [showEditContactModal, setShowEditContactModal] = useState(false);
  const [editingContactId, setEditingContactId] = useState(null);
  const [editContactData, setEditContactData] = useState({ PhoneNumber: '', FirstName: '', LastName: '', CustomFieldsJson: '{}', CheckInCount: 0, AverageStayDays: 0, IsVip: false });
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [hiddenColumns, setHiddenColumns] = useState([]);
  const [logs, setLogs] = useState([]);
  const [templatesList, setTemplatesList] = useState([]);
  const [totalTemplates, setTotalTemplates] = useState(0);
  const [rulesList, setRulesList] = useState([]);
  const [broadcasts, setBroadcasts] = useState([]);
  const [bPage, setBPage] = useState(1);
  const [bTotal, setBTotal] = useState(0);
  const bLimit = 10;
  const [selectedBroadcastCategory, setSelectedBroadcastCategory] = useState('');
  const [selectedBroadcastTemplate, setSelectedBroadcastTemplate] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [isFiring, setIsFiring] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const [testPhoneNumber, setTestPhoneNumber] = useState('+15556585587');
  const [testModalOpen, setTestModalOpen] = useState(false);
  const [templateToTest, setTemplateToTest] = useState(null);
  const [isTesting, setIsTesting] = useState(false);
  const [campaigns, setCampaigns] = useState([]);
  const [showCampaignModal, setShowCampaignModal] = useState(false);
  const [editingCampaign, setEditingCampaign] = useState(null);
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
  const [newTemplate, setNewTemplate] = useState({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp', BodyParamCount: -1, Category: 'Utility', HeaderType: 'None', BodyText: '', HeaderText: '', FooterText: '', ButtonsJson: '[]', ParameterDefinitionsJson: '[]' });
  const [isBuildingRule, setIsBuildingRule] = useState(false);
  const [editingRule, setEditingRule] = useState(null);
  const [optOutsList, setOptOutsList] = useState([]);

  // Part B State Variables
  const [conversations, setConversations] = useState([]);
  const [selectedConversation, setSelectedConversation] = useState(null);
  const [chatMessages, setChatMessages] = useState([]);
  const [showNewConversationModal, setShowNewConversationModal] = useState(false);
  const [newConversationPhone, setNewConversationPhone] = useState('');
  const [newConversationName, setNewConversationName] = useState('');
  const [replyText, setReplyText] = useState('');
  const [isSendingReply, setIsSendingReply] = useState(false);
  const [analyticsData, setAnalyticsData] = useState({ TotalTriggered: 0, Sent: 0, Delivered: 0, Read: 0, Failed: 0, Pending: 0, IncomingReplies: 0, Clicked: 0 });
  const [deliveryTrends, setDeliveryTrends] = useState([]);
  const [analyticsPeriod, setAnalyticsPeriod] = useState('all');
  const [analyticsRulesPage, setAnalyticsRulesPage] = useState(1);
  const [analyticsBroadcastsPage, setAnalyticsBroadcastsPage] = useState(1);
  const [errorReplyMessage, setErrorReplyMessage] = useState('');

  // Scroll handler ref for chat thread
  const messagesEndRef = React.useRef(null);
  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  const prevMessagesLengthRef = React.useRef(0);
  const prevConversationRef = React.useRef(null);

  useEffect(() => {
    const isNewConversation = prevConversationRef.current !== selectedConversation;
    const isNewMessage = chatMessages.length > prevMessagesLengthRef.current;
    
    if (chatMessages.length > 0 && (isNewConversation || isNewMessage)) {
      scrollToBottom();
    }
    
    prevMessagesLengthRef.current = chatMessages.length;
    prevConversationRef.current = selectedConversation;
  }, [chatMessages, selectedConversation]);

  useEffect(() => {
    fetchData();
    const pollInterval = activeTab === 'inbox' ? 1000 : 5000;
    const interval = setInterval(() => {
      fetchData();
    }, pollInterval);
    return () => clearInterval(interval);
  }, [activeTab, currentPage, pageSize, selectedCategory, selectedSmartList, bPage, analyticsPeriod, selectedConversation]);

  const fetchData = async () => {
    try {
      if (activeTab === 'optouts') {
        const res = await fetch(`${API_BASE}/unsubscribes`).catch(() => null);
        if (res && res.ok) setOptOutsList(await res.json());
      }

      const outboxRes = await fetch(`${API_BASE}/outbox`).catch(() => null);
      if (outboxRes && outboxRes.ok) {
        const outboxData = await outboxRes.json();
        setOutbox(outboxData);
        setStats(s => ({ ...s, outbox: outboxData.length }));
      }
      
      if (activeTab === 'templates' || activeTab === 'broadcasts' || activeTab === 'analytics') {
        const tLimit = activeTab === 'templates' ? pageSize : 1000;
        const tPage = activeTab === 'templates' ? currentPage : 1;
        const templatesRes = await fetch(`${API_BASE}/templates?page=${tPage}&limit=${tLimit}`).catch(() => null);
        if (templatesRes && templatesRes.ok) {
          const tData = await templatesRes.json();
          setTemplatesList(tData.Templates || tData.templates || []);
          if (activeTab === 'templates') setTotalTemplates(tData.TotalCount || tData.totalCount || 0);
        }
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
      
      if (activeTab === 'campaigns') {
        const campRes = await fetch(`${API_BASE}/campaigns`).catch(() => null);
        if (campRes && campRes.ok) setCampaigns(await campRes.json());
      }

      if (activeTab === 'broadcasts' || activeTab === 'analytics') {
        const bRes = await fetch(`${API_BASE}/broadcast?page=${bPage}&limit=${bLimit}`).catch(() => null);
        if (bRes && bRes.ok) {
          const d = await bRes.json();
          setBroadcasts(d.data || []);
          setBTotal(d.total || 0);
        }
      }
      
      const inboxRes = await fetch(`${API_BASE}/inbox`).catch(() => null);
      if (inboxRes && inboxRes.ok) {
        const inboxData = await inboxRes.json();
        setInbox(inboxData);
        setStats(s => ({ ...s, inbox: inboxData.length }));
      }

      const catRes = await fetch(`${API_BASE}/categories`).catch(() => null);
      if (catRes && catRes.ok) setCategories(await catRes.json());
      
      const slRes = await fetch(`${API_BASE}/smart-lists`).catch(() => null);
      if (slRes && slRes.ok) setSmartLists(await slRes.json());
      
      let conUrl = `${API_BASE}/contacts?page=${currentPage}&limit=${pageSize}`;
      if (selectedSmartList) {
        conUrl = `${API_BASE}/smart-lists/${selectedSmartList}/contacts?page=${currentPage}&limit=${pageSize}`;
      } else if (selectedCategory) {
        conUrl = `${API_BASE}/contacts?categoryId=${selectedCategory}&page=${currentPage}&limit=${pageSize}`;
      }
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

      // Fetch active conversations list
      if (activeTab === 'inbox') {
        const convRes = await fetch(`${API_BASE}/conversations`).catch(() => null);
        if (convRes && convRes.ok) {
          const convData = await convRes.json();
          setConversations(convData);
          
          // Keep active conversation messages in sync
          if (selectedConversation) {
            const msgRes = await fetch(`${API_BASE}/conversations/${encodeURIComponent(selectedConversation.PhoneNumber || selectedConversation.phoneNumber)}/messages`).catch(() => null);
            if (msgRes && msgRes.ok) {
              setChatMessages(await msgRes.json());
            }
          }
        }
      }

      // Fetch advanced analytics aggregates
      if (activeTab === 'analytics') {
        let queryParams = '';
        if (analyticsPeriod !== 'all') {
          const end = new Date();
          const start = new Date();
          if (analyticsPeriod === 'today') {
            start.setHours(0,0,0,0);
          } else if (analyticsPeriod === '7') {
            start.setDate(start.getDate() - 7);
          } else if (analyticsPeriod === '30') {
            start.setDate(start.getDate() - 30);
          }
          queryParams = `?startDate=${start.toISOString().split('T')[0]}&endDate=${end.toISOString().split('T')[0]}`;
        }
        
        const overviewRes = await fetch(`${API_BASE}/analytics/overview${queryParams}`).catch(() => null);
        if (overviewRes && overviewRes.ok) {
          setAnalyticsData(await overviewRes.json());
        }
        const trendsRes = await fetch(`${API_BASE}/analytics/delivery-trends${queryParams}`).catch(() => null);
        if (trendsRes && trendsRes.ok) {
          setDeliveryTrends(await trendsRes.json());
        }
      }
    } catch (e) {
      console.error(e);
    }
  };

  const handleSaveSmartList = async () => {
    try {
      const res = await fetch(`${API_BASE}/smart-lists`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ListName: newSmartList.ListName, FilterConditionsJson: JSON.stringify(newSmartList.Filters) })
      });
      if (res.ok) {
        setShowSmartListModal(false);
        setNewSmartList({ ListName: '', Filters: [{ field: 'City', operator: 'equals', value: '' }] });
        fetchData();
      } else {
        alert("Failed to save smart list.");
      }
    } catch(e) {
      alert("Error saving smart list.");
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
            CheckInCount: c.CheckInCount || c.checkInCount || 0,
            AverageStayDays: c.AverageStayDays || c.averageStayDays || 0,
            IsVip: c.IsVip || c.isVip || false,
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
            CheckInCount: 0,
            AverageStayDays: 0,
            IsVip: false,
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
              onChange={e => { setSelectedCategory(e.target.value); setSelectedSmartList(''); setCurrentPage(1); }} 
              style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
            >
              <option value="">All Categories</option>
              {(Array.isArray(categories) ? categories : []).map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
            </select>
            <button className="btn btn-secondary" onClick={handleAddCategory}>
              <Plus size={16} /> New Category
            </button>
            <div style={{ width: '1px', height: '24px', backgroundColor: '#ccc', margin: '0 8px' }}></div>
            <select 
              value={selectedSmartList} 
              onChange={e => { setSelectedSmartList(e.target.value); setSelectedCategory(''); setCurrentPage(1); }} 
              style={{ padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
            >
              <option value="">All Smart Lists</option>
              {(Array.isArray(smartLists) ? smartLists : []).map(s => <option key={s.Id || s.id} value={s.Id || s.id}>{s.ListName || s.listName}</option>)}
            </select>
            <button className="btn btn-secondary" onClick={() => setShowSmartListModal(true)}>
              <Plus size={16} /> New Smart List
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

          {showSmartListModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '500px' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>Create Smart List</h3>
                <div style={{ marginBottom: '12px' }}>
                  <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>List Name</label>
                  <input type="text" className="input" placeholder="e.g. VIP Guests" value={newSmartList.ListName} onChange={e => setNewSmartList({ ...newSmartList, ListName: e.target.value })} />
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, fontSize: '0.85em' }}>Filters</label>
                  {newSmartList.Filters.map((f, i) => (
                    <div key={i} style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                      <select className="input" value={f.field} onChange={e => {
                        const newF = [...newSmartList.Filters];
                        newF[i].field = e.target.value;
                        setNewSmartList({ ...newSmartList, Filters: newF });
                      }} style={{ flex: 1 }}>
                        <option value="City">City</option>
                        <option value="State">State</option>
                        <option value="Country">Country</option>
                        <option value="CategoryId">Category ID</option>
                      </select>
                      <select className="input" value={f.operator} onChange={e => {
                        const newF = [...newSmartList.Filters];
                        newF[i].operator = e.target.value;
                        setNewSmartList({ ...newSmartList, Filters: newF });
                      }} style={{ width: '100px' }}>
                        <option value="equals">Equals</option>
                        <option value="contains">Contains</option>
                      </select>
                      <input type="text" className="input" placeholder="Value" value={f.value} onChange={e => {
                        const newF = [...newSmartList.Filters];
                        newF[i].value = e.target.value;
                        setNewSmartList({ ...newSmartList, Filters: newF });
                      }} style={{ flex: 1 }} />
                      <button className="btn btn-secondary" style={{ color: '#ef4444' }} onClick={() => {
                        const newF = [...newSmartList.Filters];
                        newF.splice(i, 1);
                        setNewSmartList({ ...newSmartList, Filters: newF });
                      }}><Trash2 size={14} /></button>
                    </div>
                  ))}
                  <button className="btn btn-secondary" style={{ marginTop: '8px', width: '100%' }} onClick={() => setNewSmartList({ ...newSmartList, Filters: [...newSmartList.Filters, { field: 'City', operator: 'equals', value: '' }] })}>
                    <Plus size={16} style={{ marginRight: '4px' }} /> Add Condition
                  </button>
                </div>
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                  <button className="btn btn-secondary" onClick={() => setShowSmartListModal(false)}>Cancel</button>
                  <button className="btn btn-primary" onClick={handleSaveSmartList}>Save Smart List</button>
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
                      <th>Added Date</th>
                      <th>Updated Date</th>
                      <th>Updates</th>
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
                          <td>{new Date(c.CreatedAt || c.createdAt).toLocaleDateString()}</td>
                          <td>{c.UpdatedAt || c.updatedAt ? new Date(c.UpdatedAt || c.updatedAt).toLocaleDateString() : '-'}</td>
                          <td>{c.UpdateCount || c.updateCount || 0}</td>
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

                <div style={{ display: 'flex', gap: '16px', marginBottom: '16px', alignItems: 'center' }}>
                  <div className="form-group" style={{ flex: 1 }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Check-in Count</label>
                    <input type="number" min="0" value={editContactData.CheckInCount} onChange={e => setEditContactData({...editContactData, CheckInCount: parseInt(e.target.value) || 0})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  </div>
                  <div className="form-group" style={{ flex: 1 }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Avg Stay (Days)</label>
                    <input type="number" min="0" step="0.1" value={editContactData.AverageStayDays} onChange={e => setEditContactData({...editContactData, AverageStayDays: parseFloat(e.target.value) || 0})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                  </div>
                  <div className="form-group" style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                    <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>VIP</label>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '4px' }}>
                      <span style={{ fontSize: '14px', color: editContactData.IsVip ? '#64748b' : '#334155' }}>No</span>
                      <label style={{ position: 'relative', display: 'inline-block', width: '40px', height: '24px' }}>
                        <input type="checkbox" checked={editContactData.IsVip} onChange={e => setEditContactData({...editContactData, IsVip: e.target.checked})} style={{ opacity: 0, width: 0, height: 0 }} />
                        <span style={{ position: 'absolute', cursor: 'pointer', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: editContactData.IsVip ? '#10b981' : '#ccc', transition: '.4s', borderRadius: '34px' }}>
                          <span style={{ position: 'absolute', content: '""', height: '16px', width: '16px', left: editContactData.IsVip ? '20px' : '4px', bottom: '4px', backgroundColor: 'white', transition: '.4s', borderRadius: '50%' }}></span>
                        </span>
                      </label>
                      <span style={{ fontSize: '14px', color: editContactData.IsVip ? '#10b981' : '#64748b', fontWeight: editContactData.IsVip ? 'bold' : 'normal' }}>Yes</span>
                    </div>
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
                            while(newKey in fields) {
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
        let defsJson = t.ParameterDefinitionsJson || t.parameterDefinitionsJson || '[]';
        let defs = [];
        try { defs = JSON.parse(defsJson); } catch(e){}
        
        const count = t.BodyParamCount ?? t.bodyParamCount ?? -1;
        if ((!defs || defs.length === 0) && count > 0) {
          defs = Array.from({ length: count }, (_, i) => ({
            Position: i + 1,
            Label: `Variable ${i + 1}`,
            SampleValue: '',
            SourceColumn: ''
          }));
          defsJson = JSON.stringify(defs);
        }

        setNewTemplate({
          Name: t.Name || t.name || '',
          ExternalTemplateName: t.ExternalTemplateName || t.externalTemplateName || '',
          LanguageCode: t.LanguageCode || t.languageCode || 'en',
          ChannelType: t.ChannelType || t.channelType || 'WhatsApp',
          BodyParamCount: count,
          Category: t.Category || t.category || 'Utility',
          HeaderType: t.HeaderType || t.headerType || 'None',
          BodyText: t.BodyText || t.bodyText || '',
          HeaderText: t.HeaderText || t.headerText || '',
          ParameterDefinitionsJson: defsJson
        });
        setEditingTemplateId(t.Id || t.id);
        setIsEditingTemplate(true);
        setShowTemplateModal(true);
      };

      const handleSendTest = async () => {
        if (!testPhoneNumber) {
          alert('Phone number is required');
          return;
        }
        setIsTesting(true);
        try {
          const res = await fetch(`${API_BASE}/templates/${templateToTest.Id || templateToTest.id}/test`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: testPhoneNumber })
          });
          const data = await res.json();
          if (res.ok) {
            alert(data.message || 'Test sent successfully');
            setTestModalOpen(false);
          } else {
            alert(data.message || 'Failed to send test');
          }
        } catch (err) {
          alert('Error sending test');
        } finally {
          setIsTesting(false);
        }
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
              setNewTemplate({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp', BodyParamCount: -1, Category: 'Utility', HeaderType: 'None', BodyText: '', ParameterDefinitionsJson: '[]' });
              setIsEditingTemplate(false);
              setEditingTemplateId(null);
              setShowTemplateModal(true);
            }}>
              <Plus size={16} /> Create Template
            </button>
          </div>
          
          {showTemplateModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
              <div className="card" style={{ width: '850px', maxWidth: '95vw', maxHeight: '90vh', display: 'flex', flexDirection: 'column', padding: '24px', overflow: 'hidden' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border-color, #eee)', paddingBottom: '12px', marginBottom: '16px' }}>
                  <h3 className="card-title" style={{ margin: 0 }}>{isEditingTemplate ? 'Edit Message Template' : 'New Message Template'}</h3>
                  <button className="btn btn-secondary" style={{ minWidth: 'auto', padding: '4px 8px', border: 'none' }} onClick={() => setShowTemplateModal(false)}>✕</button>
                </div>
                
                <div style={{ display: 'flex', gap: '24px', flex: 1, overflow: 'auto' }}>
                  {/* Left Column: Form & Parameter Table */}
                  <div style={{ flex: 1.3, display: 'flex', flexDirection: 'column', gap: '16px', overflowY: 'auto', paddingRight: '8px' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Display Name (e.g. CheckIn Welcome)</label>
                        <input type="text" className="input" placeholder="Display Name" value={newTemplate.Name} onChange={e => setNewTemplate({...newTemplate, Name: e.target.value})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Meta Template Name</label>
                        <input type="text" className="input" placeholder="e.g. hotel_checkin" value={newTemplate.ExternalTemplateName} onChange={e => setNewTemplate({...newTemplate, ExternalTemplateName: e.target.value})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Language (e.g. en_US)</label>
                        <input type="text" className="input" placeholder="en" value={newTemplate.LanguageCode} onChange={e => setNewTemplate({...newTemplate, LanguageCode: e.target.value})} style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Channel Type</label>
                        <select className="input" value={newTemplate.ChannelType} onChange={e => setNewTemplate({...newTemplate, ChannelType: e.target.value})} style={{ width: '100%', padding: '8px 24px 8px 8px', borderRadius: '4px', border: '1px solid #ccc' }}>
                          <option value="WhatsApp">WhatsApp</option>
                          <option value="SMS">SMS</option>
                        </select>
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Category</label>
                        <select className="input" value={newTemplate.Category || 'Utility'} onChange={e => setNewTemplate({...newTemplate, Category: e.target.value})} style={{ width: '100%', padding: '8px 24px 8px 8px', borderRadius: '4px', border: '1px solid #ccc' }}>
                          <option value="Utility">Utility (Transactional)</option>
                          <option value="Marketing">Marketing (Promotional)</option>
                        </select>
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '12px' }}>
                      <div>
                        <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>Header Media Type</label>
                        <select className="input" value={newTemplate.HeaderType || 'None'} onChange={e => setNewTemplate({...newTemplate, HeaderType: e.target.value})} style={{ width: '100%', padding: '8px 24px 8px 8px', borderRadius: '4px', border: '1px solid #ccc' }}>
                          <option value="None">None</option>
                          <option value="Text">Text</option>
                          <option value="Document">Document (PDF Invoice generation)</option>
                          <option value="Image">Image</option>
                          <option value="Video">Video</option>
                        </select>
                      </div>
                      
                      {newTemplate.HeaderType && newTemplate.HeaderType !== 'None' && (
                        <div>
                          <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>
                            {newTemplate.HeaderType === 'Text' && 'Header Text'}
                            {newTemplate.HeaderType === 'Image' && 'Header Image URL'}
                            {newTemplate.HeaderType === 'Video' && 'Header Video URL'}
                            {newTemplate.HeaderType === 'Document' && 'Document URL (Optional - defaults to PDF invoice generator)'}
                          </label>
                          <input 
                            type="text" 
                            className="input" 
                            placeholder={
                              newTemplate.HeaderType === 'Text' ? "Enter header text (e.g. Booking Confirmed)" : "https://domain.com/media-file"
                            }
                            value={newTemplate.HeaderText || ''} 
                            onChange={e => setNewTemplate({...newTemplate, HeaderText: e.target.value})} 
                            style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} 
                          />
                        </div>
                      )}
                    </div>

                    <div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                        <label style={{ fontWeight: 500, fontSize: '0.85em' }}>Body Text</label>
                        <span style={{ fontSize: '0.8em', color: 'var(--text-secondary, #64748b)' }}>Use <code>{"{{1}}"}</code>, <code>{"{{2}}"}</code> for placeholders</span>
                      </div>
                      <textarea 
                        className="input" 
                        rows={5} 
                        placeholder="Hello {{1}}, welcome to Sankalp Retreat! Your room number is {{2}}." 
                        value={newTemplate.BodyText || ''} 
                        onChange={e => {
                          const body = e.target.value;
                          // Auto detect parameter positions
                          const matches = [...body.matchAll(/\{\{(\d+)\}\}/g)];
                          const positions = [...new Set(matches.map(m => parseInt(m[1])))].sort((a,b) => a-b);
                          
                          let currentDefs = [];
                          try {
                            currentDefs = JSON.parse(newTemplate.ParameterDefinitionsJson || '[]');
                          } catch(ex){}
                          if (!Array.isArray(currentDefs)) currentDefs = [];

                          const updatedDefs = positions.map(pos => {
                            const existing = currentDefs.find(d => (d.Position || d.position) === pos);
                            return {
                              Position: pos,
                              Label: existing ? (existing.Label || existing.label || `Variable ${pos}`) : `Variable ${pos}`,
                              SampleValue: existing ? (existing.SampleValue || existing.sampleValue || '') : '',
                              SourceColumn: existing ? (existing.SourceColumn || existing.sourceColumn || '') : '',
                              DynamicKey: existing ? (existing.DynamicKey || existing.dynamicKey || '') : ''
                            };
                          });

                          setNewTemplate({
                            ...newTemplate,
                            BodyText: body,
                            ParameterDefinitionsJson: JSON.stringify(updatedDefs),
                            BodyParamCount: updatedDefs.length
                          });
                        }} 
                        style={{ width: '100%', padding: '10px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace', resize: 'vertical' }} 
                      />
                    </div>

                    {/* Parameter Configuration Table */}
                    <div>
                      <h4 style={{ margin: '0 0 8px 0', fontSize: '0.9em', fontWeight: 600 }}>Configure Parameters ({newTemplate.BodyParamCount > 0 ? newTemplate.BodyParamCount : 0})</h4>
                      {(() => {
                        let defs = [];
                        try {
                          defs = JSON.parse(newTemplate.ParameterDefinitionsJson || '[]');
                        } catch(ex){}
                        if (!Array.isArray(defs) || defs.length === 0) {
                          return <div style={{ fontSize: '0.85em', color: 'var(--text-secondary, #64748b)', padding: '12px', border: '1px dashed #ccc', borderRadius: '4px', textAlign: 'center' }}>No variables detected in body text. Type <code>{"{{1}}"}</code> above to add parameters.</div>;
                        }

                        return (
                          <div style={{ border: '1px solid var(--border-color, #eee)', borderRadius: '6px', overflow: 'hidden' }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85em' }}>
                              <thead>
                                <tr style={{ backgroundColor: 'var(--bg-secondary, #f8fafc)', borderBottom: '1px solid var(--border-color, #eee)', textAlign: 'left' }}>
                                  <th style={{ padding: '8px' }}>Pos</th>
                                  <th style={{ padding: '8px' }}>Variable Label</th>
                                  <th style={{ padding: '8px' }}>Sample Value</th>
                                  <th style={{ padding: '8px' }}>Map to Column</th>
                                </tr>
                              </thead>
                              <tbody>
                                {defs.map((def, idx) => (
                                  <tr key={def.Position || def.position} style={{ borderBottom: '1px solid var(--border-color, #eee)' }}>
                                    <td style={{ padding: '8px', fontWeight: 'bold' }}>{"{{"}{def.Position || def.position}{"}}"}</td>
                                    <td style={{ padding: '8px' }}>
                                      <input 
                                        type="text" 
                                        value={def.Label || def.label || ''} 
                                        onChange={e => {
                                          const val = e.target.value;
                                          const next = defs.map((d, i) => i === idx ? { ...d, Label: val } : d);
                                          setNewTemplate({ ...newTemplate, ParameterDefinitionsJson: JSON.stringify(next) });
                                        }}
                                        style={{ width: '100%', padding: '4px', border: '1px solid #ccc', borderRadius: '3px' }} 
                                        placeholder="e.g. GuestName"
                                      />
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                      <input 
                                        type="text" 
                                        value={def.SampleValue || def.sampleValue || ''} 
                                        onChange={e => {
                                          const val = e.target.value;
                                          const next = defs.map((d, i) => i === idx ? { ...d, SampleValue: val } : d);
                                          setNewTemplate({ ...newTemplate, ParameterDefinitionsJson: JSON.stringify(next) });
                                        }}
                                        style={{ width: '100%', padding: '4px', border: '1px solid #ccc', borderRadius: '3px' }} 
                                        placeholder="John Doe"
                                      />
                                    </td>
                                    <td style={{ padding: '8px', minWidth: '180px' }}>
                                      {/* Mapping type selector */}
                                      <select
                                        value={(() => {
                                          const dynKey = def.DynamicKey ?? def.dynamicKey;
                                          if (dynKey && dynKey !== '') {
                                            const predefinedKeys = ['Name', 'Room', 'CheckInDate', 'CheckOutDate', 'BookingReference', 'WifiPassword', 'Amount', 'FeedbackLink', 'HotelName'];
                                            if (predefinedKeys.includes(dynKey)) {
                                              return `__dynamic__:${dynKey}`;
                                            }
                                            return '__dynamic__';
                                          }
                                          return def.SourceColumn || def.sourceColumn || '';
                                        })()}
                                        onChange={e => {
                                          const val = e.target.value;
                                          let next;
                                          if (val === '__dynamic__') {
                                            next = defs.map((d, i) => {
                                              if (i === idx) {
                                                const currentDynKey = d.DynamicKey ?? d.dynamicKey;
                                                const newD = { ...d, SourceColumn: '', DynamicKey: (currentDynKey && currentDynKey !== '') ? currentDynKey : ' ' };
                                                delete newD.sourceColumn;
                                                delete newD.dynamicKey;
                                                return newD;
                                              }
                                              return d;
                                            });
                                          } else if (val.startsWith('__dynamic__:')) {
                                            const key = val.split(':')[1];
                                            next = defs.map((d, i) => {
                                              if (i === idx) {
                                                const newD = { ...d, SourceColumn: '', DynamicKey: key };
                                                delete newD.sourceColumn;
                                                delete newD.dynamicKey;
                                                return newD;
                                              }
                                              return d;
                                            });
                                          } else {
                                            next = defs.map((d, i) => {
                                              if (i === idx) {
                                                const newD = { ...d, SourceColumn: val, DynamicKey: '' };
                                                delete newD.sourceColumn;
                                                delete newD.dynamicKey;
                                                return newD;
                                              }
                                              return d;
                                            });
                                          }
                                          setNewTemplate({ ...newTemplate, ParameterDefinitionsJson: JSON.stringify(next) });
                                        }}
                                        style={{ width: '100%', padding: '4px', border: '1px solid var(--border-color, #ccc)', borderRadius: '3px', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)', marginBottom: (def.DynamicKey || def.dynamicKey) ? '4px' : '0' }}
                                      >
                                        <option value="">-- None --</option>
                                        <option value="__dynamic__">📡 Event / API Variable</option>
                                        <option disabled>── Contact Fields ──</option>
                                        <option value="PhoneNumber">Phone Number</option>
                                        <option value="FirstName">First Name</option>
                                        <option value="LastName">Last Name</option>
                                        {(Array.isArray(dynamicHeaders) ? dynamicHeaders : []).map(h => (
                                          <option key={h} value={h}>{h}</option>
                                        ))}
                                        <option disabled>── Event Fields ──</option>
                                        <option value="__dynamic__:Name">Guest Name</option>
                                        <option value="__dynamic__:Room">Room Number</option>
                                        <option value="__dynamic__:CheckInDate">Check-In Date</option>
                                        <option value="__dynamic__:CheckOutDate">Check-Out Date</option>
                                        <option value="__dynamic__:BookingReference">Booking Reference</option>
                                        <option value="__dynamic__:WifiPassword">Wi-Fi Password</option>
                                        <option value="__dynamic__:Amount">Bill Amount</option>
                                        <option value="__dynamic__:FeedbackLink">Feedback Link</option>
                                        <option value="__dynamic__:HotelName">Hotel Name</option>
                                      </select>
                                      {/* Dynamic key text input — shown when Event Variable is selected */}
                                      {(() => {
                                        const dynKey = def.DynamicKey ?? def.dynamicKey;
                                        const isDynamic = dynKey !== undefined && dynKey !== null && dynKey !== '';
                                        if (!isDynamic) return null;
                                        return (
                                          <input
                                            type="text"
                                            value={dynKey.trim()}
                                            onChange={e => {
                                              const val = e.target.value;
                                              const next = defs.map((d, i) => {
                                                if (i === idx) {
                                                  const newD = { ...d, DynamicKey: val === '' ? ' ' : val };
                                                  delete newD.dynamicKey;
                                                  return newD;
                                                }
                                                return d;
                                              });
                                              setNewTemplate({ ...newTemplate, ParameterDefinitionsJson: JSON.stringify(next) });
                                            }}
                                            placeholder="e.g. CheckInDate"
                                            style={{ width: '100%', padding: '4px', border: '1px solid var(--primary, #3b82f6)', borderRadius: '3px', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)', fontSize: '0.8em' }}
                                          />
                                        );
                                      })()}
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        );
                      })()}
                    </div>
                    {/* Footer Configuration */}
                    <div>
                      <label style={{ fontWeight: 500, fontSize: '0.85em', display: 'block', marginBottom: '4px' }}>Footer Text (Optional)</label>
                      <input 
                        type="text"
                        className="input"
                        placeholder="e.g. Reply STOP to unsubscribe"
                        value={newTemplate.FooterText || ''}
                        onChange={e => setNewTemplate({...newTemplate, FooterText: e.target.value})}
                        style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                      />
                    </div>

                    {/* Buttons Configuration */}
                    <div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                        <label style={{ fontWeight: 500, fontSize: '0.85em' }}>Buttons (Optional)</label>
                        <button 
                          className="btn btn-secondary" 
                          style={{ padding: '4px 8px', fontSize: '0.75em' }}
                          onClick={() => {
                            let btns = [];
                            try { btns = JSON.parse(newTemplate.ButtonsJson || '[]'); } catch(e){}
                            if (btns.length < 10) {
                              btns.push({ type: 'QUICK_REPLY', text: '' });
                              setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(btns) });
                            }
                          }}
                        >
                          + Add Button
                        </button>
                      </div>
                      {(() => {
                        let btns = [];
                        try { btns = JSON.parse(newTemplate.ButtonsJson || '[]'); } catch(e){}
                        if (!Array.isArray(btns) || btns.length === 0) return null;
                        return (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                            {btns.map((btn, idx) => (
                              <div key={idx} style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                                <select 
                                  className="input"
                                  value={btn.type}
                                  onChange={e => {
                                    const next = [...btns];
                                    next[idx].type = e.target.value;
                                    if (e.target.value !== 'URL') delete next[idx].url;
                                    if (e.target.value !== 'PHONE_NUMBER') delete next[idx].phone_number;
                                    setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                  }}
                                  style={{ padding: '6px', width: '150px', fontSize: '0.8em' }}
                                >
                                  <option value="QUICK_REPLY">Quick Reply</option>
                                  <option value="URL">Visit Website</option>
                                  <option value="PHONE_NUMBER">Call Number</option>
                                </select>
                                <input 
                                  type="text" 
                                  className="input"
                                  placeholder="Button Text (max 25 chars)"
                                  value={btn.text || ''}
                                  maxLength={25}
                                  onChange={e => {
                                    const next = [...btns];
                                    next[idx].text = e.target.value;
                                    setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                  }}
                                  style={{ padding: '6px', flex: 1, fontSize: '0.8em' }}
                                />
                                {btn.type === 'URL' && (
                                  <input 
                                    type="text" 
                                    className="input"
                                    placeholder="URL (https://...)"
                                    value={btn.url || ''}
                                    onChange={e => {
                                      const next = [...btns];
                                      next[idx].url = e.target.value;
                                      setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                    }}
                                    style={{ padding: '6px', flex: 1, fontSize: '0.8em' }}
                                  />
                                )}
                                {btn.type === 'PHONE_NUMBER' && (
                                  <input 
                                    type="text" 
                                    className="input"
                                    placeholder="+123456789"
                                    value={btn.phone_number || ''}
                                    onChange={e => {
                                      const next = [...btns];
                                      next[idx].phone_number = e.target.value;
                                      setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                    }}
                                    style={{ padding: '6px', flex: 1, fontSize: '0.8em' }}
                                  />
                                )}
                                <button 
                                  style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', fontSize: '1.2em' }}
                                  onClick={() => {
                                    const next = btns.filter((_, i) => i !== idx);
                                    setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                  }}
                                >
                                  &times;
                                </button>
                              </div>
                            ))}
                          </div>
                        );
                      })()}
                    </div>
                  </div>

                  {/* Right Column: WhatsApp Live Preview */}
                  <div style={{ flex: 0.9, backgroundColor: '#efeae2', borderRadius: '8px', padding: '16px', display: 'flex', flexDirection: 'column', position: 'relative', minHeight: '350px', backgroundImage: 'url("https://user-images.githubusercontent.com/15075759/28719144-86dc0f70-73b1-11e7-911d-60d70fcded21.png")' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px', background: '#075e54', padding: '8px 12px', color: '#fff', borderRadius: '6px 6px 0 0', position: 'absolute', top: 0, left: 0, right: 0 }}>
                      <div style={{ width: '32px', height: '32px', borderRadius: '50%', backgroundColor: '#ece5dd', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#075e54', fontWeight: 'bold', fontSize: '0.9em' }}>SR</div>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: '0.85em' }}>Sankalp Retreat</div>
                        <div style={{ fontSize: '0.65em', opacity: 0.8 }}>Online</div>
                      </div>
                    </div>

                    <div style={{ flex: 1, display: 'flex', alignItems: 'flex-start', justifyContent: 'flex-start', padding: '12px', marginTop: '48px', overflowY: 'auto' }}>
                      <div style={{ backgroundColor: '#fff', padding: '8px 12px', borderRadius: '8px', boxShadow: '0 1px 0.5px rgba(0,0,0,0.13)', maxWidth: '85%', fontSize: '0.85em', position: 'relative', wordBreak: 'break-word' }}>
                        {newTemplate.HeaderType && newTemplate.HeaderType !== 'None' && (
                          <div style={{ borderBottom: '1px solid #f2f2f2', paddingBottom: '6px', marginBottom: '6px', fontSize: '0.9em' }}>
                            {newTemplate.HeaderType === 'Text' && (
                              <div style={{ fontWeight: 'bold', color: '#1e293b', marginBottom: '4px' }}>
                                {newTemplate.HeaderText || 'WhatsApp Header [Text]'}
                              </div>
                            )}
                            {newTemplate.HeaderType === 'Image' && (
                              <div style={{ width: '100%', height: '100px', backgroundColor: '#f1f5f9', borderRadius: '4px', display: 'flex', alignItems: 'center', justifyContent: 'center', overflow: 'hidden', marginBottom: '4px' }}>
                                {newTemplate.HeaderText ? (
                                  <img src={newTemplate.HeaderText} alt="Header Preview" style={{ width: '100%', height: '100%', objectFit: 'cover' }} onError={(e) => { e.target.style.display = 'none'; }} />
                                ) : (
                                  <span style={{ fontSize: '0.8em', color: '#94a3b8' }}>🖼️ Image Header Preview</span>
                                )}
                              </div>
                            )}
                            {newTemplate.HeaderType === 'Video' && (
                              <div style={{ width: '100%', height: '100px', backgroundColor: '#f1f5f9', borderRadius: '4px', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '4px', fontSize: '0.8em', color: '#94a3b8' }}>
                                🎥 Video Header: {newTemplate.HeaderText || 'No media URL provided'}
                              </div>
                            )}
                            {newTemplate.HeaderType === 'Document' && (
                              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '6px 8px', backgroundColor: '#f1f5f9', borderRadius: '4px', marginBottom: '4px', fontSize: '0.8em', color: '#475569' }}>
                                📄 <strong>Document:</strong> {newTemplate.HeaderText || 'Dynamic Generated Invoice.pdf'}
                              </div>
                            )}
                          </div>
                        )}
                        <div style={{ whiteSpace: 'pre-wrap' }}>
                          {(() => {
                            let text = newTemplate.BodyText || 'Welcome template preview...';
                            let defs = [];
                            try {
                              defs = JSON.parse(newTemplate.ParameterDefinitionsJson || '[]');
                            } catch(ex){}
                            if (Array.isArray(defs)) {
                              defs.forEach(d => {
                                const pos = d.Position || d.position;
                                const val = d.SampleValue || d.sampleValue || `{{${pos}}}`;
                                text = text.replaceAll(`{{${pos}}}`, `*${val}*`);
                              });
                            }
                            return text;
                          })()}
                        </div>
                        {newTemplate.FooterText && (
                          <div style={{ fontSize: '0.7em', color: '#8696a0', marginTop: '6px' }}>
                            {newTemplate.FooterText}
                          </div>
                        )}
                        <div style={{ display: 'flex', justifyContent: 'flex-end', fontSize: '0.65em', color: '#8696a0', marginTop: '4px' }}>
                          {new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </div>
                      </div>
                    </div>

                    {/* Buttons Preview */}
                    {(() => {
                      let btns = [];
                      try { btns = JSON.parse(newTemplate.ButtonsJson || '[]'); } catch(e){}
                      if (!Array.isArray(btns) || btns.length === 0) return null;
                      return (
                        <div style={{ padding: '0 12px 12px 12px', display: 'flex', flexDirection: 'column', gap: '4px', maxWidth: '85%' }}>
                          {btns.map((btn, idx) => (
                            <div key={idx} style={{ backgroundColor: '#fff', padding: '10px 12px', borderRadius: '8px', boxShadow: '0 1px 0.5px rgba(0,0,0,0.13)', textAlign: 'center', fontSize: '0.85em', color: '#00a884', cursor: 'pointer' }}>
                              {btn.type === 'URL' && <span style={{ marginRight: '6px' }}>↗️</span>}
                              {btn.type === 'PHONE_NUMBER' && <span style={{ marginRight: '6px' }}>📞</span>}
                              {btn.type === 'QUICK_REPLY' && <span style={{ marginRight: '6px' }}>↩️</span>}
                              {btn.text || 'Button Text'}
                            </div>
                          ))}
                        </div>
                      );
                    })()}
                  </div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', borderTop: '1px solid var(--border-color, #eee)', paddingTop: '16px', marginTop: '16px' }}>
                  <button className="btn btn-secondary" onClick={() => setShowTemplateModal(false)}>Cancel</button>
                  <button className="btn btn-primary" onClick={handleSaveTemplate}>Save Template</button>
                </div>
              </div>
            </div>
          ), document.body)}

          {testModalOpen && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="card" style={{ width: '400px', animation: 'fadeIn 0.2s ease-out' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>Test Template: {templateToTest?.Name || templateToTest?.name}</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  <label style={{ fontSize: '0.9rem', color: 'var(--text-secondary)' }}>Enter a phone number with country code (e.g. 919876543210)</label>
                  <input type="text" className="input" placeholder="Phone Number" value={testPhoneNumber} onChange={e => setTestPhoneNumber(e.target.value)} style={{ padding: '10px', borderRadius: '6px', border: '1px solid var(--border-color)', width: '100%' }} />
                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '16px' }}>
                    <button className="btn btn-secondary" onClick={() => setTestModalOpen(false)} disabled={isTesting}>Cancel</button>
                    <button className="btn btn-primary" onClick={handleSendTest} disabled={isTesting || !testPhoneNumber}>
                      {isTesting ? 'Sending...' : 'Send Test'}
                    </button>
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
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#10b981', borderColor: '#10b981' }} title="Test Template" onClick={() => {
                          setTemplateToTest(t);
                          setTestModalOpen(true);
                        }}>
                          <Send size={14} />
                        </button>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => handleEditTemplate(t)} title="Edit Template">
                          <Edit2 size={14} />
                        </button>
                        <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#ef4444' }} onClick={() => handleDeleteTemplate(t.Id || t.id)} title="Delete Template">
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalTemplates > 0 && (
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', padding: '16px', borderTop: '1px solid #e2e8f0', backgroundColor: '#fff', borderRadius: '0 0 8px 8px' }}>
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
                <span style={{ color: '#64748b', marginLeft: '16px' }}>Total: {totalTemplates}</span>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <button 
                  className="btn btn-secondary" 
                  disabled={currentPage === 1} 
                  onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                  style={{ padding: '6px 12px' }}
                >
                  Previous
                </button>
                <span style={{ fontWeight: 500 }}>
                  Page {currentPage} of {Math.ceil(totalTemplates / pageSize) || 1}
                </span>
                <button 
                  className="btn btn-secondary" 
                  disabled={currentPage >= Math.ceil(totalTemplates / pageSize)} 
                  onClick={() => setCurrentPage(p => p + 1)}
                  style={{ padding: '6px 12px' }}
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </div>
      );
    }

    if (activeTab === 'campaigns') {
      return (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '24px' }}>
            <h2 style={{ fontSize: '1.5em', fontWeight: 600 }}>Drip Campaigns</h2>
            <button className="btn btn-primary" onClick={() => {
              setEditingCampaign({ Name: '', Steps: [] });
              setShowCampaignModal(true);
            }}>
              <Plus size={18} style={{ marginRight: '8px' }} /> Create Campaign
            </button>
          </div>

          <div className="card">
            {campaigns.length === 0 ? (
              <div className="empty-state">No campaigns found. Create one to get started!</div>
            ) : (
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Campaign Name</th>
                    <th>Steps</th>
                    <th>Status</th>
                    <th>Created At</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {campaigns.map(c => (
                    <tr key={c.Id || c.id}>
                      <td style={{ fontWeight: 500 }}>{c.Name || c.name}</td>
                      <td>{(c.Steps || c.steps || []).length}</td>
                      <td>
                        <span style={{ 
                          padding: '4px 8px', borderRadius: '12px', fontSize: '0.85em', fontWeight: 500,
                          backgroundColor: (c.IsEnabled || c.isEnabled) ? '#dcfce7' : '#f1f5f9',
                          color: (c.IsEnabled || c.isEnabled) ? '#166534' : '#475569'
                        }}>
                          {(c.IsEnabled || c.isEnabled) ? 'Active' : 'Paused'}
                        </span>
                      </td>
                      <td>{new Date(c.CreatedAt || c.createdAt).toLocaleString()}</td>
                      <td>
                        <div style={{ display: 'flex', gap: '8px' }}>
                          <button className="btn btn-secondary" style={{ padding: '4px 8px' }} onClick={() => {
                            setEditingCampaign(c);
                            setShowCampaignModal(true);
                          }}>
                            <Edit2 size={14} />
                          </button>
                          <button className="btn btn-secondary" style={{ padding: '4px 8px', color: '#ef4444' }} onClick={async () => {
                            if (window.confirm('Delete this campaign?')) {
                              await fetch(`${API_BASE}/campaigns/${c.Id || c.id}`, { method: 'DELETE' });
                              fetchData();
                            }
                          }}>
                            <Trash2 size={14} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          {showCampaignModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, overflowY: 'auto', padding: '20px' }}>
              <div className="card" style={{ width: '600px', maxHeight: '90vh', overflowY: 'auto' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>{editingCampaign?.Id || editingCampaign?.id ? 'Edit Campaign' : 'New Campaign'}</h3>
                
                <div style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500 }}>Campaign Name</label>
                  <input type="text" className="input" value={editingCampaign?.Name || editingCampaign?.name || ''} onChange={e => setEditingCampaign({ ...editingCampaign, Name: e.target.value })} placeholder="e.g., Post-Checkout Feedback Drip" style={{ width: '100%' }} />
                </div>

                <div style={{ marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <input type="checkbox" id="campEnabled" checked={editingCampaign?.IsEnabled ?? editingCampaign?.isEnabled ?? true} onChange={e => setEditingCampaign({ ...editingCampaign, IsEnabled: e.target.checked })} />
                  <label htmlFor="campEnabled" style={{ fontWeight: 500 }}>Campaign is Active</label>
                </div>

                <div style={{ marginBottom: '16px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                    <h4 style={{ margin: 0 }}>Campaign Steps</h4>
                    <button className="btn btn-secondary" onClick={() => {
                      const steps = [...(editingCampaign?.Steps || editingCampaign?.steps || [])];
                      steps.push({ StepNumber: steps.length + 1, TemplateId: templatesList[0]?.Id || templatesList[0]?.id || 0, DelayMinutes: 0 });
                      setEditingCampaign({ ...editingCampaign, Steps: steps });
                    }}>
                      <Plus size={14} style={{ marginRight: '4px' }} /> Add Step
                    </button>
                  </div>
                  
                  {(editingCampaign?.Steps || editingCampaign?.steps || []).map((step, idx) => (
                    <div key={idx} style={{ padding: '12px', border: '1px solid #e2e8f0', borderRadius: '6px', marginBottom: '8px', backgroundColor: '#f8fafc' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                        <span style={{ fontWeight: 600 }}>Step {idx + 1}</span>
                        <button style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer' }} onClick={() => {
                          const steps = [...(editingCampaign?.Steps || editingCampaign?.steps || [])];
                          steps.splice(idx, 1);
                          // Re-number
                          steps.forEach((s, i) => s.StepNumber = i + 1);
                          setEditingCampaign({ ...editingCampaign, Steps: steps });
                        }}>
                          <Trash2 size={14} />
                        </button>
                      </div>
                      <div style={{ display: 'flex', gap: '12px', marginBottom: '8px' }}>
                        <div style={{ flex: 1 }}>
                          <label style={{ display: 'block', fontSize: '0.85em', color: '#64748b', marginBottom: '4px' }}>Template</label>
                          <select className="input" value={step.TemplateId || step.templateId || ''} onChange={e => {
                            const steps = [...(editingCampaign?.Steps || editingCampaign?.steps || [])];
                            steps[idx].TemplateId = parseInt(e.target.value);
                            setEditingCampaign({ ...editingCampaign, Steps: steps });
                          }} style={{ width: '100%' }}>
                            {(templatesList || []).map(t => <option key={t.Id || t.id} value={t.Id || t.id}>{t.Name || t.name}</option>)}
                          </select>
                        </div>
                        <div style={{ width: '150px' }}>
                          <label style={{ display: 'block', fontSize: '0.85em', color: '#64748b', marginBottom: '4px' }}>Delay (Minutes)</label>
                          <input type="number" className="input" value={step.DelayMinutes || step.delayMinutes || 0} onChange={e => {
                            const steps = [...(editingCampaign?.Steps || editingCampaign?.steps || [])];
                            steps[idx].DelayMinutes = parseInt(e.target.value);
                            setEditingCampaign({ ...editingCampaign, Steps: steps });
                          }} style={{ width: '100%' }} />
                        </div>
                      </div>
                    </div>
                  ))}
                  {(editingCampaign?.Steps || editingCampaign?.steps || []).length === 0 && (
                    <div style={{ fontSize: '0.9em', color: '#64748b', textAlign: 'center', padding: '16px', border: '1px dashed #cbd5e1', borderRadius: '4px' }}>
                      No steps configured. Add a step to send messages.
                    </div>
                  )}
                </div>

                <div style={{ display: 'flex', justifyContent: 'space-between', borderTop: '1px solid #e2e8f0', paddingTop: '16px' }}>
                  <button className="btn btn-secondary" onClick={() => {
                    const campId = editingCampaign?.Id || editingCampaign?.id;
                    if (!campId) return alert('Save campaign first!');
                    const slId = prompt('Enter Smart List ID to enroll:');
                    if (slId) {
                      fetch(`${API_BASE}/campaigns/${campId}/enroll?smartListId=${slId}`, { method: 'POST' })
                        .then(r => r.json()).then(res => alert(`Enrolled ${res.EnrolledCount || res.enrolledCount} contacts!`));
                    }
                  }} style={{ color: '#0284c7' }}>
                    <Users size={16} style={{ marginRight: '6px' }} /> Enroll Smart List
                  </button>

                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button className="btn btn-secondary" onClick={() => setShowCampaignModal(false)}>Cancel</button>
                    <button className="btn btn-primary" onClick={async () => {
                      const method = editingCampaign.Id || editingCampaign.id ? 'PUT' : 'POST';
                      const url = editingCampaign.Id || editingCampaign.id ? `${API_BASE}/campaigns/${editingCampaign.Id || editingCampaign.id}` : `${API_BASE}/campaigns`;
                      const res = await fetch(url, {
                        method,
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(editingCampaign)
                      });
                      if (res.ok) {
                        setShowCampaignModal(false);
                        fetchData();
                      } else {
                        alert('Error saving campaign');
                      }
                    }}>Save Campaign</button>
                  </div>
                </div>
              </div>
            </div>
          ), document.body)}
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
            <h3 className="card-title">Automation</h3>
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
          const res = await fetch(`${API_BASE}/rules/${rule.Id || rule.id}/analytics-details`);
          if (res.ok) setRuleFailures(await res.json());
        } catch (e) { console.error(e); }
      };

      const handleShowBroadcastAnalytics = async (b) => {
        setAnalyticsRule({ ...b, Name: b.TemplateName || b.templateName, isBroadcast: true });
        setRuleFailures([]);
        try {
          const res = await fetch(`${API_BASE}/broadcast/analytics-details?templateId=${b.TemplateId || b.templateId}&startedAt=${encodeURIComponent(b.StartedAt || b.startedAt)}`);
          if (res.ok) setRuleFailures(await res.json());
        } catch (e) { console.error(e); }
      };

      const overallTriggered = analyticsData?.TotalTriggered || 0;
      const overallSent = (analyticsData?.Sent || 0) + (analyticsData?.Delivered || 0) + (analyticsData?.Read || 0);
      const overallFailed = analyticsData?.Failed || 0;
      const successRate = overallTriggered > 0 ? ((overallSent / (overallSent + overallFailed || 1)) * 100).toFixed(1) : '0.0';



      const analyticsPageSize = 10;
      const rulesSafe = rulesList || [];
      const currentRulesPage = rulesSafe.slice((analyticsRulesPage - 1) * analyticsPageSize, analyticsRulesPage * analyticsPageSize);

      const broadcastsSafe = broadcasts || [];
      const currentBroadcastsPage = broadcastsSafe.slice((analyticsBroadcastsPage - 1) * analyticsPageSize, analyticsBroadcastsPage * analyticsPageSize);

      return (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
            <h2 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 'bold' }}>Analytics Overview</h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
              <span style={{ fontSize: '0.9rem', color: '#64748b', fontWeight: 600 }}>Date Range:</span>
              <select 
                className="input" 
                style={{ padding: '6px 12px', width: '200px' }}
                value={analyticsPeriod}
                onChange={(e) => setAnalyticsPeriod(e.target.value)}
              >
                <option value="today">Today</option>
                <option value="7">Last 7 Days</option>
                <option value="30">Last 30 Days</option>
                <option value="all">All Time</option>
              </select>
              <button className="btn btn-secondary" onClick={() => window.location.href = `${API_BASE}/analytics/export`} style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Download size={16} /> Export CSV
              </button>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '32px' }}>
            <div className="card" style={{ gridColumn: 'span 2', borderLeft: '4px solid #3b82f6' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>OVERALL TRIGGERED</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{overallTriggered}</div>
            </div>
            <div className="card" style={{ gridColumn: 'span 2', borderLeft: '4px solid #10b981' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>SUCCESSFULLY SENT</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#10b981' }}>{overallSent}</div>
            </div>
            <div className="card" style={{ gridColumn: 'span 2', borderLeft: '4px solid #ef4444' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>FAILED DELIVERIES</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#ef4444' }}>{overallFailed}</div>
            </div>
            <div className="card" style={{ gridColumn: 'span 2', borderLeft: '4px solid #eab308' }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600 }}>DELIVERY SUCCESS RATE</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#eab308' }}>{successRate}%</div>
            </div>
          </div>

          <div className="card" style={{ marginBottom: '32px' }}>
            <h3 className="card-title" style={{ marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Workflow size={20} color="var(--primary)" /> Automation Performance
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
                  {currentRulesPage.map(r => {
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
                            <BarChart2 size={12} /> View Details
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                  {currentRulesPage.length === 0 && (
                    <tr>
                      <td colSpan="7" style={{ textAlign: 'center', padding: '24px' }}>No automation rules found.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px', borderTop: '1px solid var(--border)' }}>
              <div style={{ color: '#64748b', fontSize: '0.9rem' }}>Total: {rulesSafe.length}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <button 
                  className="btn btn-secondary" 
                  disabled={analyticsRulesPage === 1} 
                  onClick={() => setAnalyticsRulesPage(p => Math.max(1, p - 1))}
                  style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                >
                  Previous
                </button>
                <span style={{ fontWeight: 500, fontSize: '0.9rem' }}>Page {analyticsRulesPage} of {Math.ceil(rulesSafe.length / analyticsPageSize) || 1}</span>
                <button 
                  className="btn btn-secondary" 
                  disabled={analyticsRulesPage >= Math.ceil(rulesSafe.length / analyticsPageSize)} 
                  onClick={() => setAnalyticsRulesPage(p => p + 1)}
                  style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                >
                  Next
                </button>
              </div>
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
                    <th style={{ textAlign: 'center' }}>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {currentBroadcastsPage.map((b, i) => {
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
                        <td style={{ textAlign: 'center' }}>
                          <button 
                            className="btn btn-secondary" 
                            style={{ padding: '6px 12px', fontSize: '0.8rem', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                            onClick={() => handleShowBroadcastAnalytics(b)}
                          >
                            <BarChart2 size={12} /> View Details
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                  {currentBroadcastsPage.length === 0 && (
                    <tr>
                      <td colSpan="8" style={{ textAlign: 'center', padding: '24px' }}>No broadcast campaigns found.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '16px', borderTop: '1px solid var(--border)' }}>
              <div style={{ color: '#64748b', fontSize: '0.9rem' }}>Total: {broadcastsSafe.length}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <button 
                  className="btn btn-secondary" 
                  disabled={analyticsBroadcastsPage === 1} 
                  onClick={() => setAnalyticsBroadcastsPage(p => Math.max(1, p - 1))}
                  style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                >
                  Previous
                </button>
                <span style={{ fontWeight: 500, fontSize: '0.9rem' }}>Page {analyticsBroadcastsPage} of {Math.ceil(broadcastsSafe.length / analyticsPageSize) || 1}</span>
                <button 
                  className="btn btn-secondary" 
                  disabled={analyticsBroadcastsPage >= Math.ceil(broadcastsSafe.length / analyticsPageSize)} 
                  onClick={() => setAnalyticsBroadcastsPage(p => p + 1)}
                  style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                >
                  Next
                </button>
              </div>
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

                <h4 style={{ marginBottom: '8px', color: '#334155', fontWeight: 600 }}>Delivery Details Log</h4>
                <div style={{ flex: 1, overflowY: 'auto', maxHeight: '350px', border: '1px solid #e2e8f0', borderRadius: '6px', backgroundColor: '#f8fafc', padding: '12px' }}>
                  {ruleFailures.length === 0 ? (
                    <div style={{ textAlign: 'center', color: '#64748b', padding: '24px 0' }}>
                      No messages recorded.
                    </div>
                  ) : (
                    <table className="data-table" style={{ margin: 0, backgroundColor: 'white' }}>
                      <thead>
                        <tr>
                          <th>Phone</th>
                          <th>Status</th>
                          <th>Date</th>
                          <th>Details</th>
                        </tr>
                      </thead>
                      <tbody>
                        {ruleFailures.map((f, i) => (
                          <tr key={i}>
                            <td style={{ fontWeight: 600 }}>
                              {f.ToPhoneNumber || f.toPhoneNumber}
                              {(f.IsUnsubscribed || f.isUnsubscribed) === 1 && (
                                <span style={{ marginLeft: '6px', fontSize: '10px', backgroundColor: '#ef4444', color: 'white', padding: '2px 4px', borderRadius: '4px' }}>Unsubscribed</span>
                              )}
                            </td>
                            <td>
                              <span className={`badge badge-${(f.Status || f.status || '').toLowerCase()}`}>
                                {f.Status || f.status}
                              </span>
                            </td>
                            <td style={{ fontSize: '0.85rem', color: '#64748b' }}>
                              {new Date(f.CreatedAt || f.createdAt).toLocaleString()}
                            </td>
                            <td style={{ fontSize: '0.85rem' }}>
                              {(f.Status || f.status) === 'Failed' ? (
                                <span style={{ color: '#ef4444' }}>{f.FailureReason || f.failureReason || 'Failed to connect to Meta WhatsApp API.'}</span>
                              ) : (
                                <span style={{ color: '#10b981' }}>-</span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
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

    if (activeTab === 'optouts') {
      const handleResubscribe = async (phone) => {
        if (!confirm(`Are you sure you want to re-subscribe ${phone}?`)) return;
        try {
          await fetch(`${API_BASE}/unsubscribes/${encodeURIComponent(phone)}`, { method: 'DELETE' });
          fetchData(); // refresh
        } catch (e) {
          console.error("Failed to resubscribe", e);
        }
      };

      return (
        <div className="card">
          <h3 className="card-title" style={{ marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <UserX size={20} color="var(--primary)" /> Marketing Opt-Outs (Unsubscribed)
          </h3>
          <p style={{ color: 'var(--text-secondary)', marginBottom: '16px' }}>
            Users who have replied "STOP" or opted out of "Offers and announcements" on WhatsApp. They will not receive marketing templates.
          </p>
          <div className="data-table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Phone Number</th>
                  <th>Contact Name</th>
                  <th>Unsubscribed At</th>
                  <th>Source</th>
                  <th style={{ textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {(Array.isArray(optOutsList) ? optOutsList : []).map(o => (
                  <tr key={o.PhoneNumber || o.phoneNumber}>
                    <td style={{ fontWeight: 600 }}>{o.PhoneNumber || o.phoneNumber}</td>
                    <td>{o.FirstName || o.firstName ? `${o.FirstName || o.firstName} ${o.LastName && o.LastName !== 'undefined' ? o.LastName : (o.lastName && o.lastName !== 'undefined' ? o.lastName : '')}`.trim() : '-'}</td>
                    <td>{new Date(o.UnsubscribedAt || o.unsubscribedAt).toLocaleString()}</td>
                    <td><span className="badge badge-pending">{o.Source || o.source}</span></td>
                    <td style={{ textAlign: 'center' }}>
                      <button 
                        className="btn btn-secondary" 
                        style={{ padding: '6px 12px', fontSize: '0.8rem', color: '#10b981' }}
                        onClick={() => handleResubscribe(o.PhoneNumber || o.phoneNumber)}
                        title="Re-Subscribe User"
                      >
                        Re-Subscribe
                      </button>
                    </td>
                  </tr>
                ))}
                {(optOutsList || []).length === 0 && (
                  <tr>
                    <td colSpan="5" style={{ textAlign: 'center', padding: '24px' }}>No unsubscribed contacts found.</td>
                  </tr>
                )}
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
              <div style={{ padding: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderTop: '1px solid #e2e8f0' }}>
                <button 
                  className="btn btn-secondary" 
                  disabled={bPage === 1} 
                  onClick={() => setBPage(p => p - 1)}
                  style={{ padding: '6px 12px' }}
                >
                  Previous
                </button>
                <span style={{ fontSize: '0.9em', color: '#64748b' }}>
                  Page {bPage} of {Math.ceil(bTotal / bLimit) || 1}
                </span>
                <button 
                  className="btn btn-secondary" 
                  disabled={bPage >= Math.ceil(bTotal / bLimit) || bTotal === 0} 
                  onClick={() => setBPage(p => p + 1)}
                  style={{ padding: '6px 12px' }}
                >
                  Next
                </button>
              </div>
            </div>
          </div>
        </div>
      );
    }

    if (activeTab === 'inbox') {
      const handleSelectConversation = async (conv) => {
        setSelectedConversation(conv);
        setErrorReplyMessage('');
        try {
          const res = await fetch(`${API_BASE}/conversations/${encodeURIComponent(conv.PhoneNumber || conv.phoneNumber)}/messages`);
          if (res.ok) setChatMessages(await res.json());
        } catch(e){}
      };

      const handleSendReplyMessage = async () => {
        if (!replyText.trim() || !selectedConversation) return;
        setIsSendingReply(true);
        setErrorReplyMessage('');
        const phone = selectedConversation.PhoneNumber || selectedConversation.phoneNumber;
        try {
          const res = await fetch(`${API_BASE}/conversations/${encodeURIComponent(phone)}/reply`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ body: replyText })
          });
          const data = await res.json();
          if (res.ok) {
            setReplyText('');
            // reload messages
            const msgRes = await fetch(`${API_BASE}/conversations/${encodeURIComponent(phone)}/messages`);
            if (msgRes.ok) setChatMessages(await msgRes.json());
            fetchData();
          } else {
            setErrorReplyMessage(data.message || 'Failed to send reply.');
          }
        } catch(err) {
          setErrorReplyMessage('Failed to connect to backend.');
        } finally {
          setIsSendingReply(false);
        }
      };

      return (
        <>
        <div className="card" style={{ display: 'flex', height: 'calc(100vh - 180px)', padding: 0, overflow: 'hidden' }}>
          {/* Active Conversations Left Sidebar */}
          <div style={{ width: '320px', borderRight: '1px solid var(--border-color, #eee)', display: 'flex', flexDirection: 'column', height: '100%', backgroundColor: 'var(--bg-secondary, #f8fafc)' }}>
            <div style={{ padding: '16px', borderBottom: '1px solid var(--border-color, #eee)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div style={{ fontWeight: 600, fontSize: '0.95em' }}>Conversations Inbox</div>
              <button 
                className="btn btn-primary" 
                style={{ padding: '4px 8px', fontSize: '0.8em', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '4px' }}
                onClick={() => setShowNewConversationModal(true)}
              >
                <Plus size={14} /> New
              </button>
            </div>
            <div style={{ flex: 1, overflowY: 'auto' }}>
              {(conversations || []).length === 0 ? (
                <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-secondary, #64748b)', fontSize: '0.85em' }}>No conversations yet. Send a test template to start!</div>
              ) : (
                (conversations || []).map(conv => {
                  const phone = conv.PhoneNumber || conv.phoneNumber;
                  const name = (conv.FirstName || conv.firstName) 
                    ? `${conv.FirstName || conv.firstName} ${conv.LastName && conv.LastName !== 'undefined' ? conv.LastName : (conv.lastName && conv.lastName !== 'undefined' ? conv.lastName : '')}`.trim() 
                    : phone;
                  const isSelected = selectedConversation && (selectedConversation.PhoneNumber || selectedConversation.phoneNumber) === phone;
                  const hasUnread = (conv.UnreadCount || conv.unreadCount || 0) > 0;
                  return (
                    <div 
                      key={phone} 
                      onClick={() => handleSelectConversation(conv)}
                      style={{ 
                        padding: '12px 16px', 
                        borderBottom: '1px solid var(--border-color, #eee)', 
                        cursor: 'pointer',
                        backgroundColor: isSelected ? 'var(--primary-light, #eff6ff)' : 'transparent',
                        transition: 'background-color 0.2s',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '4px'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <span style={{ fontWeight: hasUnread ? 'bold' : 500, fontSize: '0.9em', color: isSelected ? 'var(--primary, #3b82f6)' : 'inherit' }}>{name}</span>
                        {hasUnread && <span style={{ backgroundColor: 'var(--primary, #3b82f6)', color: 'white', fontSize: '0.7em', padding: '2px 6px', borderRadius: '10px', fontWeight: 'bold' }}>New</span>}
                      </div>
                      <div style={{ fontSize: '0.75em', color: 'var(--text-secondary, #64748b)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {conv.LastMessageText || conv.lastMessageText || 'No messages'}
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </div>

          {/* Conversation Chat Bubbles View */}
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', backgroundColor: '#efeae2', backgroundImage: 'url("https://user-images.githubusercontent.com/15075759/28719144-86dc0f70-73b1-11e7-911d-60d70fcded21.png")' }}>
            {selectedConversation ? (
              <>
                {/* Chat Top bar */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px', background: '#075e54', padding: '12px 20px', color: '#fff' }}>
                  <div style={{ width: '36px', height: '36px', borderRadius: '50%', backgroundColor: '#ece5dd', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#075e54', fontWeight: 'bold' }}>
                    {(selectedConversation.FirstName || selectedConversation.firstName || 'G').charAt(0).toUpperCase()}
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column' }}>
                    <div style={{ fontWeight: 600, fontSize: '0.9em' }}>
                      {(selectedConversation.FirstName || selectedConversation.firstName)
                        ? `${selectedConversation.FirstName || selectedConversation.firstName} ${selectedConversation.LastName && selectedConversation.LastName !== 'undefined' ? selectedConversation.LastName : (selectedConversation.lastName && selectedConversation.lastName !== 'undefined' ? selectedConversation.lastName : '')}`.trim()
                        : (selectedConversation.PhoneNumber || selectedConversation.phoneNumber)}
                    </div>
                    <div style={{ fontSize: '0.7em', opacity: 0.85 }}>{selectedConversation.PhoneNumber || selectedConversation.phoneNumber} • WhatsApp conversation</div>
                  </div>
                </div>

                {/* Messages scroll section */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '20px 24px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  {chatMessages.map((msg, index) => {
                    const isGuest = msg.Sender === 'Guest' || msg.sender === 'Guest';
                    return (
                      <div key={msg.Id || msg.id || index} style={{ display: 'flex', justifyContent: isGuest ? 'flex-start' : 'flex-end' }}>
                        <div style={{ 
                          backgroundColor: isGuest ? '#ffffff' : '#d9fdd3', 
                          padding: '8px 12px', 
                          borderRadius: '8px', 
                          boxShadow: '0 1px 0.5px rgba(0,0,0,0.13)', 
                          maxWidth: '75%', 
                          fontSize: '0.85em', 
                          wordBreak: 'break-word' 
                        }}>
                          <div>{msg.Content || msg.content}</div>
                          <div style={{ display: 'flex', justifyContent: 'flex-end', fontSize: '0.65em', color: '#8696a0', marginTop: '4px' }}>
                            {new Date(msg.SentAt || msg.sentAt).toLocaleString([], { hour: '2-digit', minute: '2-digit' })}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                  <div ref={messagesEndRef} />
                </div>

                {/* Meta Window and Error alerts */}
                {errorReplyMessage && (
                  <div style={{ margin: '8px 16px 0 16px', padding: '10px', backgroundColor: '#fef2f2', border: '1px solid #fee2e2', borderRadius: '6px', fontSize: '0.8em', color: '#ef4444' }}>
                    <strong>Warning:</strong> {errorReplyMessage}
                  </div>
                )}

                {/* Reply Footer Input Area */}
                <div style={{ padding: '12px 16px', backgroundColor: '#f0f2f5', display: 'flex', gap: '12px', alignItems: 'center', borderTop: '1px solid var(--border-color, #eee)' }}>
                  <input 
                    type="text" 
                    className="input" 
                    placeholder="Type message reply..." 
                    value={replyText} 
                    onChange={e => setReplyText(e.target.value)}
                    onKeyDown={e => {
                      if (e.key === 'Enter') handleSendReplyMessage();
                    }}
                    style={{ flex: 1, padding: '10px', borderRadius: '6px', border: '1px solid #ccc', backgroundColor: '#fff', color: '#000' }}
                  />

                  <button 
                    className="btn btn-primary" 
                    onClick={handleSendReplyMessage} 
                    disabled={isSendingReply || !replyText.trim()}
                    style={{ padding: '10px 16px' }}
                  >
                    <Send size={16} />
                  </button>
                </div>
              </>
            ) : (
              <div style={{ flex: 1, display: 'flex', justifyContent: 'center', alignItems: 'center', flexDirection: 'column', color: 'var(--text-secondary, #64748b)' }}>
                <MessagesSquare size={48} style={{ marginBottom: '16px' }} />
                <h3>Select a conversation to reply</h3>
                <p style={{ fontSize: '0.85em' }}>Double-click any chat to retrieve thread history.</p>
              </div>
            )}
          </div>
        </div>

        {/* New Conversation Modal */}
        {showNewConversationModal && (
          <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
            <div className="card" style={{ width: '100%', maxWidth: '420px', padding: '32px' }}>
              <h3 className="card-title" style={{ marginBottom: '20px', fontSize: '1.25rem' }}>Start New Conversation</h3>
              
              <div style={{ fontSize: '0.85em', color: 'var(--text-secondary)', backgroundColor: 'var(--bg-color)', padding: '16px', borderRadius: '8px', marginBottom: '24px', border: '1px solid var(--border-color)', lineHeight: '1.5' }}>
                <strong style={{ color: 'var(--text-primary)' }}>Note:</strong> WhatsApp allows free-form messages only if the recipient has messaged you in the last 24 hours. Otherwise, your message will be rejected by Meta. For new contacts, please use an approved template via Broadcasts.
              </div>
              
              <div className="form-group" style={{ marginBottom: '20px' }}>
                <label>Phone Number (with country code)</label>
                <input 
                  type="text" 
                  className="form-control" 
                  placeholder="e.g. 1234567890" 
                  value={newConversationPhone} 
                  onChange={e => setNewConversationPhone(e.target.value)} 
                />
              </div>

              <div className="form-group" style={{ marginBottom: '32px' }}>
                <label>First Name (Optional)</label>
                <input 
                  type="text" 
                  className="form-control" 
                  placeholder="e.g. John" 
                  value={newConversationName} 
                  onChange={e => setNewConversationName(e.target.value)} 
                  onKeyDown={e => {
                    if (e.key === 'Enter' && newConversationPhone.trim()) {
                      const mockConv = { PhoneNumber: newConversationPhone.trim(), FirstName: newConversationName.trim() || 'Guest' };
                      const existingConv = (conversations || []).find(c => (c.PhoneNumber || c.phoneNumber) === mockConv.PhoneNumber);
                      if (!existingConv) setConversations([mockConv, ...(conversations || [])]);
                      setSelectedConversation(existingConv || mockConv);
                      setChatMessages([]);
                      setShowNewConversationModal(false);
                      setNewConversationPhone('');
                      setNewConversationName('');
                    }
                  }}
                />
              </div>
              
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button 
                  className="btn btn-secondary" 
                  onClick={() => {
                    setShowNewConversationModal(false);
                    setNewConversationPhone('');
                    setNewConversationName('');
                  }}
                >
                  Cancel
                </button>
                <button 
                  className="btn btn-primary" 
                  disabled={!newConversationPhone.trim()}
                  onClick={() => {
                    const mockConv = {
                      PhoneNumber: newConversationPhone.trim(),
                      FirstName: newConversationName.trim() || 'Guest'
                    };
                    const existingConv = (conversations || []).find(c => (c.PhoneNumber || c.phoneNumber) === mockConv.PhoneNumber);
                    if (!existingConv) {
                      setConversations([mockConv, ...(conversations || [])]);
                    }
                    setSelectedConversation(existingConv || mockConv);
                    setChatMessages([]);
                    setShowNewConversationModal(false);
                    setNewConversationPhone('');
                    setNewConversationName('');
                  }}
                >
                  Start Chat
                </button>
              </div>
            </div>
          </div>
        )}
        </>
      );
    }

    if (activeTab === 'analytics') {
      const successRate = analyticsData.TotalTriggered > 0 ? (((analyticsData.Delivered + analyticsData.Sent + analyticsData.Read) / (analyticsData.TotalTriggered || 1)) * 100).toFixed(1) : '0.0';

      return (
        <div>
          {/* Card Widgets */}
          <div style={{ display: 'flex', gap: '20px', marginBottom: '32px' }}>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #3b82f6', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ color: 'var(--text-secondary, #64748b)', fontSize: '0.8rem', fontWeight: 600 }}>TOTAL DISPATCHED</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>{analyticsData.TotalTriggered}</div>
            </div>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #10b981', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ color: '#10b981', fontSize: '0.8rem', fontWeight: 600 }}>SUCCESS DELIVERED</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#10b981' }}>{analyticsData.Delivered + analyticsData.Sent + analyticsData.Read}</div>
            </div>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #ef4444', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ color: '#ef4444', fontSize: '0.8rem', fontWeight: 600 }}>FAILED OUTBOX</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#ef4444' }}>{analyticsData.Failed}</div>
            </div>
            <div className="card" style={{ flex: 1, borderLeft: '4px solid #eab308', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ color: '#eab308', fontSize: '0.8rem', fontWeight: 600 }}>OVERALL SUCCESS RATE</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold', color: '#eab308' }}>{successRate}%</div>
            </div>
          </div>

          {/* Delivery trends progress visualization */}
          <div style={{ display: 'flex', gap: '24px', marginBottom: '32px' }}>
            <div className="card" style={{ flex: 1.5 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h3 className="card-title" style={{ margin: 0 }}>Delivery Trends (Last 7 Days)</h3>
                <a 
                  href={`${API_BASE}/analytics/export`} 
                  download="MessageReport.csv"
                  className="btn btn-secondary" 
                  style={{ textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: '6px', fontSize: '0.85em', padding: '6px 12px' }}
                >
                  📥 Export CSV Report
                </a>
              </div>
              
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                {(deliveryTrends || []).length === 0 ? (
                  <div style={{ padding: '24px', textAlign: 'center', color: 'var(--text-secondary, #64748b)' }}>No historical delivery data found.</div>
                ) : (
                  (deliveryTrends || []).map((t, idx) => {
                    const total = (t.Success || 0) + (t.Failed || 0);
                    const rate = total > 0 ? ((t.Success / total) * 100).toFixed(1) : 100;
                    return (
                      <div key={idx} style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.85em', fontWeight: 500 }}>
                          <span>{t.Day || t.day}</span>
                          <span style={{ color: 'var(--text-secondary, #64748b)' }}>{t.Success} Sent / {t.Failed} Failed ({rate}%)</span>
                        </div>
                        <div style={{ width: '100%', height: '8px', backgroundColor: '#fee2e2', borderRadius: '4px', overflow: 'hidden', display: 'flex' }}>
                          <div style={{ width: `${rate}%`, height: '100%', backgroundColor: '#10b981' }}></div>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            </div>
            
            {/* Inbox widget snippet */}
            <div className="card" style={{ flex: 1 }}>
              <h3 className="card-title">Replies Ratio</h3>
              <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', height: '100%', padding: '20px 0', gap: '12px' }}>
                <div style={{ fontSize: '48px', fontWeight: 'bold', color: 'var(--primary)' }}>
                  {analyticsData.IncomingReplies}
                </div>
                <div style={{ fontSize: '0.85em', color: 'var(--text-secondary)', textAlign: 'center' }}>
                  Total incoming text responses received from guests in the Two-way Inbox.
                </div>
              </div>
            </div>
          </div>
        </div>
      );
    }

    if (activeTab === 'manual-checkin') {
      return <ManualCheckIn />;
    }

    if (activeTab === 'manual-checkout') {
      return <ManualCheckOut />;
    }

    return null;
  };

  return (
    <div className="app-container" style={{ display: 'flex' }}>
      <aside className={`sidebar ${sidebarOpen ? 'open' : 'closed'}`}>
        <div className="sidebar-header">
          <img src="/logo.png" alt="HotelAuto Logo" style={{ width: '100%', maxHeight: '60px', objectFit: 'contain' }} />
        </div>
        <nav style={{ display: 'flex', flexDirection: 'column', gap: '8px', padding: '12px 0' }}>
          <div className={`nav-item ${activeTab === 'dashboard' ? 'active' : ''}`} onClick={() => setActiveTab('dashboard')}>
            <LayoutDashboard size={20} /> Dashboard
          </div>
          <div className={`nav-item ${activeTab === 'inbox' ? 'active' : ''}`} onClick={() => setActiveTab('inbox')}>
            <MessagesSquare size={20} /> Two-Way Inbox
          </div>
          <div className={`nav-item ${activeTab === 'contacts' ? 'active' : ''}`} onClick={() => setActiveTab('contacts')}>
            <Users size={20} /> Contacts
          </div>
          <div className={`nav-item ${activeTab === 'templates' ? 'active' : ''}`} onClick={() => setActiveTab('templates')}>
            <FileText size={20} /> Templates
          </div>
          <div className={`nav-item ${activeTab === 'broadcasts' ? 'active' : ''}`} onClick={() => setActiveTab('broadcasts')}>
            <Megaphone size={20} /> Broadcasts
          </div>
          <div className={`nav-item ${activeTab === 'rules' ? 'active' : ''}`} onClick={() => setActiveTab('rules')}>
            <Workflow size={20} /> Automation
          </div>
          <div className={`nav-item ${activeTab === 'analytics' ? 'active' : ''}`} onClick={() => setActiveTab('analytics')}>
            <BarChart2 size={20} /> Analytics
          </div>
          <div className={`nav-item ${activeTab === 'optouts' ? 'active' : ''}`} onClick={() => setActiveTab('optouts')}>
            <UserX size={20} /> Opt-Outs
          </div>

          <div className="sidebar-divider" style={{ margin: '8px 16px', borderBottom: '1px solid var(--border)' }}></div>
          <div className={`nav-item ${activeTab === 'manual-checkin' ? 'active' : ''}`} onClick={() => setActiveTab('manual-checkin')}>
            <Hotel size={20} /> Manual Check-In
          </div>
          <div className={`nav-item ${activeTab === 'manual-checkout' ? 'active' : ''}`} onClick={() => setActiveTab('manual-checkout')}>
            <CheckCircle2 size={20} /> Manual Check-Out
          </div>
        </nav>
      </aside>

      <main className="main-content" style={{ flex: 1, overflowX: 'hidden', transition: 'margin-left 0.3s ease' }}>
        <header className="topbar">
          <div className="topbar-title" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <button className="btn btn-secondary" style={{ padding: '6px' }} onClick={() => setSidebarOpen(!sidebarOpen)}>
              <Menu size={20} />
            </button>
            {activeTab === 'rules' ? 'Automation' : activeTab.charAt(0).toUpperCase() + activeTab.slice(1)}
          </div>
        </header>

        <div className="page-content">
          {renderContent()}
        </div>
      </main>
    </div>
  );
}
