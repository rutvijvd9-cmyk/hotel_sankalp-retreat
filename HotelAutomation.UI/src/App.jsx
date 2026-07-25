import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import Papa from 'papaparse';
import { LayoutDashboard, MessagesSquare, Workflow, ScrollText, Send, Hotel, CheckCircle2, AlertCircle, Clock, Users, Upload, Plus, Megaphone, Edit2, Trash2, Settings, XCircle, BarChart2, UserX, Layers, FileText, Menu, Download, TrendingUp, Activity, Calendar, UserPlus, Eye, LogOut, Lock, Shield, UserCheck, ShieldAlert } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import AutomationBuilder from './components/AutomationBuilder';
import PreferenceCenter from './components/PreferenceCenter';
import TemplateVariantManager from './components/TemplateVariantManager';
import ManualCheckIn from './components/ManualCheckIn';
import ManualCheckOut from './components/ManualCheckOut';

const API_BASE = '/api';

const PERMANENT_CUSTOM_FIELDS = [
  'Registration Number',
  'Bill No',
  'Arrival Date',
  'Departure date',
  'Arrival Time',
  'Departure Time',
  'Company Name',
  'Group',
  'City Name',
  'State Name',
  'Prefix',
  'Nationality',
  'Room No',
  'Room Type',
  'Person',
  'Extra Person',
  'Expected Stay days',
  'Expected Departure Date',
  'AP',
  'MAP',
  'EP',
  'CP',
  'Discount',
  'Room Tariff',
  'Final Tariff',
  'Total',
  'Carry Date',
  'Carry registraction No',
  'Carry Amount',
  'Deposit',
  'Reservation Remark',
  'Room Change',
  'Other Remark',
  'days',
  'CGST',
  'SGST',
  'Round off',
  'Mode of Payment/Bill To',
  'Room Total',
  'C/F. Amount',
  'Deposit(-)',
  'Roon Service',
  'Laundary',
  'Misc Service',
  'Final Amount',
  'B/F Registration No',
  'C/F Registration No'
];

const parseDateSafe = (dateStr) => {
  if (!dateStr) return new Date();
  let normalized = dateStr.trim().replace(' ', 'T');
  normalized = normalized.replace(/\.(\d{1,6})/, (match, p1) => `.${p1.substring(0, 3)}`);
  const parsed = new Date(normalized);
  return isNaN(parsed.getTime()) ? new Date(dateStr) : parsed;
};

export default function App() {
  const params = new URLSearchParams(window.location.search);
  const prefContactId = params.get('preferences_for');

  // Auth States
  const [user, setUser] = useState(null);
  const [isCheckingAuth, setIsCheckingAuth] = useState(true);
  const [loginUsername, setLoginUsername] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const [loginError, setLoginError] = useState('');

  // User management states
  const [usersList, setUsersList] = useState([]);
  const [showCreateUserModal, setShowCreateUserModal] = useState(false);
  const [newUserUsername, setNewUserUsername] = useState('');
  const [newUserPassword, setNewUserPassword] = useState('');
  const [newUserRole, setNewUserRole] = useState('Employer');
  const [createUserError, setCreateUserError] = useState('');
  const [userSubTab, setUserSubTab] = useState('list'); // 'list' or 'permissions'

  const [permissions, setPermissions] = useState([]);

  const fetchPermissions = async () => {
    try {
      const res = await fetch(`${API_BASE}/permissions`);
      if (res.ok) {
        setPermissions(await res.json());
      }
    } catch (e) {}
  };

  const hasAccess = (moduleKey) => {
    if (!user) return false;
    if (user.role === 'Owner' || user.role === 'Developer') return true;
    const match = permissions.find(p => p.role.toLowerCase() === user.role.toLowerCase() && p.module.toLowerCase() === moduleKey.toLowerCase());
    return match ? match.isEnabled : true;
  };

  // Reset password states
  const [showResetPasswordModal, setShowResetPasswordModal] = useState(false);
  const [resetPasswordUserId, setResetPasswordUserId] = useState(null);
  const [resetPasswordUsername, setResetPasswordUsername] = useState('');
  const [newPasswordForUser, setNewPasswordForUser] = useState('');
  const [resetPasswordError, setResetPasswordError] = useState('');

  const [activeTab, setActiveTab] = useState(() => {
    const tab = localStorage.getItem('hotelAuto_activeTab');
    return tab === 'inbox' ? 'dashboard' : (tab || 'dashboard');
  });
  
  useEffect(() => {
    localStorage.setItem('hotelAuto_activeTab', activeTab);
  }, [activeTab]);

  // Auth Handlers
  const checkAuth = async () => {
    try {
      const res = await fetch(`${API_BASE}/auth/me`);
      if (res.ok) {
        const uData = await res.json();
        setUser(uData);
        fetchPermissions();
      } else {
        setUser(null);
      }
    } catch (e) {
      setUser(null);
    } finally {
      setIsCheckingAuth(false);
    }
  };

  useEffect(() => {
    checkAuth();
  }, []);

  useEffect(() => {
    if (user) {
      fetchPermissions();
    }
  }, [user]);

  const handleApiError = (res) => {
    if (res && res.status === 401) {
      setUser(null);
    }
  };

  const handleLogin = async (e) => {
    e.preventDefault();
    setLoginError('');
    try {
      const res = await fetch(`${API_BASE}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Username: loginUsername, Password: loginPassword })
      });
      if (res.ok) {
        setUser(await res.json());
        // Fetch initial data right away
        fetchData();
      } else {
        const err = await res.json().catch(() => ({ error: 'Invalid credentials' }));
        setLoginError(err.error || 'Invalid credentials');
      }
    } catch (e) {
      setLoginError('Connection to backend failed');
    }
  };

  const handleLogout = async () => {
    try {
      await fetch(`${API_BASE}/auth/logout`, { method: 'POST' });
    } catch (e) {}
    setUser(null);
    setActiveTab('dashboard');
  };

  const fetchUsers = async () => {
    try {
      const res = await fetch(`${API_BASE}/users`);
      if (res.ok) {
        setUsersList(await res.json());
      } else {
        handleApiError(res);
      }
    } catch (e) {}
  };

  useEffect(() => {
    if (user && activeTab === 'users') {
      fetchUsers();
    }
  }, [activeTab, user]);

  const handleToggleUserStatus = async (id, isActive) => {
    try {
      const res = await fetch(`${API_BASE}/users/${id}/status`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ IsActive: isActive })
      });
      if (res.ok) {
        fetchUsers();
      } else {
        const err = await res.json().catch(() => ({ error: 'Action failed' }));
        alert(err.error || 'Action failed');
      }
    } catch (e) {}
  };

  const handleUpdateUserRole = async (id, newRole) => {
    try {
      const res = await fetch(`${API_BASE}/users/${id}/role`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Role: newRole })
      });
      if (res.ok) {
        fetchUsers();
      } else {
        const err = await res.json().catch(() => ({ error: 'Action failed' }));
        alert(err.error || 'Action failed');
      }
    } catch (e) {}
  };

  const handleCreateUser = async (e) => {
    e.preventDefault();
    setCreateUserError('');
    try {
      const res = await fetch(`${API_BASE}/users`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Username: newUserUsername, Password: newUserPassword, Role: newUserRole })
      });
      if (res.ok) {
        setShowCreateUserModal(false);
        setNewUserUsername('');
        setNewUserPassword('');
        setNewUserRole('Employer');
        fetchUsers();
      } else {
        const err = await res.json().catch(() => ({ error: 'Failed to create user' }));
        setCreateUserError(err.error || 'Failed to create user');
      }
    } catch (e) {
      setCreateUserError('Request failed');
    }
  };

  const handleResetPassword = async (e) => {
    e.preventDefault();
    setResetPasswordError('');
    try {
      const res = await fetch(`${API_BASE}/users/${resetPasswordUserId}/reset-password`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Password: newPasswordForUser })
      });
      if (res.ok) {
        setShowResetPasswordModal(false);
        setNewPasswordForUser('');
        alert('Password reset successfully.');
      } else {
        const err = await res.json().catch(() => ({ error: 'Action failed' }));
        setResetPasswordError(err.error || 'Action failed');
      }
    } catch (e) {
      setResetPasswordError('Request failed');
    }
  };

  const handleDeleteUser = async (id, username) => {
    if (!confirm(`Are you sure you want to permanently delete the user account "${username}"?`)) {
      return;
    }
    try {
      const res = await fetch(`${API_BASE}/users/${id}`, {
        method: 'DELETE'
      });
      if (res.ok) {
        fetchUsers();
      } else {
        const err = await res.json().catch(() => ({ error: 'Action failed' }));
        alert(err.error || 'Action failed');
      }
    } catch (e) {}
  };

  const handleImpersonateUser = async (id) => {
    try {
      const res = await fetch(`${API_BASE}/auth/impersonate/${id}`, { method: 'POST' });
      if (res.ok) {
        const data = await res.json();
        setUser(data);
        setActiveTab('dashboard'); // Redirect to dashboard to check modules
      } else {
        const err = await res.json().catch(() => ({ error: 'Action failed' }));
        alert(err.error || 'Action failed');
      }
    } catch (e) {}
  };

  const handleStopImpersonating = async () => {
    try {
      const res = await fetch(`${API_BASE}/auth/stop-impersonating`, { method: 'POST' });
      if (res.ok) {
        const data = await res.json();
        setUser(data);
        fetchPermissions();
        setActiveTab('users'); // Go back to User Accounts management
      }
    } catch (e) {}
  };

  const handleTogglePermission = async (role, moduleKey, isEnabled) => {
    try {
      const res = await fetch(`${API_BASE}/permissions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ Role: role, Module: moduleKey, IsEnabled: isEnabled })
      });
      if (res.ok) {
        fetchPermissions();
      }
    } catch (e) {}
  };

  const handleUploadSignature = async (userId, file) => {
    if (!file) return;
    const formData = new FormData();
    formData.append('file', file);
    try {
      const res = await fetch(`${API_BASE}/users/${userId}/signature`, {
        method: 'POST',
        body: formData
      });
      if (res.ok) {
        alert('Signature uploaded successfully!');
        fetchUsers();
      } else {
        const err = await res.json().catch(() => ({ error: 'Upload failed' }));
        alert(err.error || 'Upload failed');
      }
    } catch (e) {
      alert('Upload failed');
    }
  };

  const [stats, setStats] = useState({ templates: 0, rules: 0, outbox: 0 });
  const [outbox, setOutbox] = useState([]);
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
  const [showDisplayColumnsModal, setShowDisplayColumnsModal] = useState(false);
  const [logs, setLogs] = useState([]);
  const [templatesList, setTemplatesList] = useState([]);
  const [totalTemplates, setTotalTemplates] = useState(0);
  const [isSyncing, setIsSyncing] = useState(false);
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

  const [analyticsData, setAnalyticsData] = useState({ TotalTriggered: 0, Sent: 0, Delivered: 0, Read: 0, Failed: 0, Pending: 0, IncomingReplies: 0, Clicked: 0 });
  const [deliveryTrends, setDeliveryTrends] = useState([]);
  const [analyticsPeriod, setAnalyticsPeriod] = useState('all');
  const [analyticsRulesPage, setAnalyticsRulesPage] = useState(1);
  const [analyticsBroadcastsPage, setAnalyticsBroadcastsPage] = useState(1);
  useEffect(() => {
    fetchData();
    const interval = setInterval(() => {
      fetchData();
    }, 5000);
    return () => clearInterval(interval);
  }, [activeTab, currentPage, pageSize, selectedCategory, selectedSmartList, bPage, analyticsPeriod]);

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
      // Compute Dynamic Chart Data & Trends (Last 7 Days vs Previous 7 Days)
      const now = new Date();
      const getDaysAgo = (dateStr) => {
        if (!dateStr) return 999;
        const d = new Date(dateStr);
        return Math.floor((now - d) / (1000 * 60 * 60 * 24));
      };

      let outboxThisWeek = 0;
      let outboxLastWeek = 0;
      const chartData = [];

      for (let i = 6; i >= 0; i--) {
        const d = new Date();
        d.setDate(d.getDate() - i);
        chartData.push({ name: d.toLocaleDateString('en-US', { weekday: 'short' }), messages: 0, dayOffset: i });
      }

      (Array.isArray(outbox) ? outbox : []).forEach(msg => {
        const daysAgo = getDaysAgo(msg.CreatedAt || msg.createdAt);
        if (daysAgo <= 6) {
          outboxThisWeek++;
          const chartItem = chartData.find(c => c.dayOffset === daysAgo);
          if (chartItem) chartItem.messages++;
        } else if (daysAgo > 6 && daysAgo <= 13) {
          outboxLastWeek++;
        }
      });

      const calcTrend = (current, previous) => {
        if (previous === 0 && current > 0) return { val: 100, type: 'up' };
        if (previous === 0 && current === 0) return { val: 0, type: 'neutral' };
        const percent = ((current - previous) / previous) * 100;
        return { val: Math.abs(percent).toFixed(1), type: percent > 0 ? 'up' : percent < 0 ? 'down' : 'neutral' };
      };

      const outTrend = calcTrend(outboxThisWeek, outboxLastWeek);

      return (
        <div className="premium-dashboard-container">
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '24px' }}>
            <div className="premium-card">
              <div className="premium-card-header">
                <div>
                  <div className="premium-metric-value">{typeof stats.outbox === 'object' ? 0 : stats.outbox}</div>
                  <div className="premium-metric-label">Total Messages Sent</div>
                </div>
                <div className="premium-icon-box">
                  <Send size={24} />
                </div>
              </div>
              <div className={`premium-trend ${outTrend.type}`}>
                {outTrend.type === 'up' && <TrendingUp size={16} />}
                {outTrend.type === 'down' && <TrendingUp size={16} style={{transform: 'rotate(180deg)'}} />}
                {outTrend.type === 'neutral' && <Activity size={16} />}
                <span>{outTrend.type === 'neutral' ? 'No change' : `${outTrend.type === 'up' ? '+' : '-'}${outTrend.val}% this week`}</span>
              </div>
            </div>

            <div className="premium-card">
              <div className="premium-card-header">
                <div>
                  <div className="premium-metric-value">{typeof stats.rules === 'object' ? 0 : stats.rules}</div>
                  <div className="premium-metric-label">Active Automations</div>
                </div>
                <div className="premium-icon-box gold">
                  <Workflow size={24} />
                </div>
              </div>
              <div className="premium-trend neutral">
                <Activity size={16} />
                <span>Running smoothly</span>
              </div>
            </div>

          </div>

          <div className="premium-card" style={{ height: '400px' }}>
            <div className="premium-section-title">
              <BarChart2 size={20} style={{ color: 'var(--primary)' }} />
              Weekly Message Volume
            </div>
            <div style={{ width: '100%', height: '300px' }}>
              <ResponsiveContainer>
                <BarChart data={chartData} margin={{ top: 20, right: 30, left: 0, bottom: 5 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(0,0,0,0.05)" />
                  <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fill: 'var(--text-secondary)', fontSize: 12 }} dy={10} />
                  <YAxis axisLine={false} tickLine={false} tick={{ fill: 'var(--text-secondary)', fontSize: 12 }} dx={-10} />
                  <Tooltip 
                    cursor={{ fill: 'rgba(59, 130, 246, 0.05)' }}
                    contentStyle={{ borderRadius: '12px', border: 'none', boxShadow: '0 4px 20px rgba(0,0,0,0.08)' }}
                  />
                  <Bar dataKey="messages" fill="var(--primary)" radius={[6, 6, 6, 6]} barSize={40} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '24px' }}>
            <div className="premium-card">
              <div className="premium-section-title">
                <Send size={20} style={{ color: 'var(--primary)' }} />
                Recent Outbox
              </div>
              {outbox.length === 0 ? (
                <div className="empty-state">No outbox messages found.</div>
              ) : (
                <table className="premium-table">
                  <thead>
                    <tr>
                      <th>Phone Number</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(Array.isArray(outbox) ? outbox : []).slice(0, 5).map(msg => (
                      <tr key={msg.Id || msg.id}>
                        <td style={{ fontFamily: 'monospace', fontWeight: 500 }}>{msg.ToPhoneNumber || msg.toPhoneNumber}</td>
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
        PERMANENT_CUSTOM_FIELDS.forEach(f => defaultFields[f] = '');
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
        PERMANENT_CUSTOM_FIELDS.forEach(f => defaultFields[f] = '');
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
        <div className="premium-dashboard-container">
          <div className="premium-toolbar">
            <select 
              value={selectedCategory} 
              onChange={e => { setSelectedCategory(e.target.value); setSelectedSmartList(''); setCurrentPage(1); }} 
            >
              <option value="">All Categories</option>
              {(Array.isArray(categories) ? categories : []).map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
            </select>
            <button className="btn btn-secondary" onClick={handleAddCategory}>
              <Plus size={16} /> New Category
            </button>
            <div className="premium-toolbar-divider"></div>
            <select 
              value={selectedSmartList} 
              onChange={e => { setSelectedSmartList(e.target.value); setSelectedCategory(''); setCurrentPage(1); }} 
            >
              <option value="">All Smart Lists</option>
              {(Array.isArray(smartLists) ? smartLists : []).map(s => <option key={s.Id || s.id} value={s.Id || s.id}>{s.ListName || s.listName}</option>)}
            </select>
            <button className="btn btn-secondary" onClick={() => setShowSmartListModal(true)}>
              <Plus size={16} /> New Smart List
            </button>
            <div style={{ flex: 1 }}></div>
            <button className="btn btn-secondary" onClick={() => setShowDisplayColumnsModal(true)}>
              <Eye size={16} /> Columns
            </button>
            <button className="btn btn-secondary" onClick={() => setShowColumnModal(true)}>
              <Settings size={16} /> Manage Columns
            </button>
            <button className="btn btn-secondary" onClick={handleAddContactClick}>
              <Plus size={16} /> Add Contact
            </button>
            <button className="btn btn-secondary" onClick={handleDeleteAllContacts} style={{ color: 'var(--danger)', borderColor: 'var(--border-color)' }}>
              <Trash2 size={16} /> Delete All
            </button>
            <label className="btn btn-primary" style={{ cursor: isImporting ? 'not-allowed' : 'pointer', opacity: isImporting ? 0.7 : 1 }}>
              {isImporting ? 'Loading CSV...' : <><Upload size={16} /> Import CSV</>}
              <input type="file" accept=".csv" style={{ display: 'none' }} onChange={handleFileSelect} disabled={isImporting} />
            </label>
          </div>

          {showColumnModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ width: '400px', animation: 'fadeIn 0.2s ease-out' }}>
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

          {showDisplayColumnsModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ width: '450px', maxHeight: '90vh', display: 'flex', flexDirection: 'column', animation: 'fadeIn 0.2s ease-out' }}>
                <h3 className="card-title" style={{ marginBottom: '12px' }}>Display Columns</h3>
                <p style={{ fontSize: '0.85em', color: 'var(--text-secondary)', marginBottom: '16px' }}>
                  Choose which columns you want to show or hide in the contacts directory.
                </p>
                
                <div style={{ flex: 1, overflowY: 'auto', paddingRight: '4px', marginBottom: '16px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  <div style={{ fontWeight: 600, fontSize: '0.9em', color: 'var(--text-primary)', borderBottom: '1px solid var(--border-color)', paddingBottom: '4px' }}>Standard Fields</div>
                  {['Phone', 'First Name', 'Last Name', 'Added Date', 'Updated Date', 'Updates'].map(col => {
                    const isVisible = !hiddenColumns.includes(col);
                    return (
                      <label key={col} style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer', fontSize: '0.95em', userSelect: 'none' }}>
                        <input 
                          type="checkbox" 
                          checked={isVisible} 
                          onChange={() => {
                            if (isVisible) {
                              setHiddenColumns([...hiddenColumns, col]);
                            } else {
                              setHiddenColumns(hiddenColumns.filter(c => c !== col));
                            }
                          }}
                          style={{ width: '16px', height: '16px', cursor: 'pointer' }}
                        />
                        <span>{col}</span>
                      </label>
                    );
                  })}

                  <div style={{ fontWeight: 600, fontSize: '0.9em', color: 'var(--text-primary)', borderBottom: '1px solid var(--border-color)', paddingBottom: '4px', marginTop: '10px' }}>Custom Fields</div>
                  {dynamicHeaders.length === 0 ? (
                    <div style={{ fontSize: '0.9em', color: 'var(--text-secondary)', fontStyle: 'italic', paddingLeft: '4px' }}>No custom fields found.</div>
                  ) : (
                    dynamicHeaders.map(col => {
                      const isVisible = !hiddenColumns.includes(col);
                      return (
                        <label key={col} style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer', fontSize: '0.95em', userSelect: 'none' }}>
                          <input 
                            type="checkbox" 
                            checked={isVisible} 
                            onChange={() => {
                              if (isVisible) {
                                setHiddenColumns([...hiddenColumns, col]);
                              } else {
                                setHiddenColumns(hiddenColumns.filter(c => c !== col));
                              }
                            }}
                            style={{ width: '16px', height: '16px', cursor: 'pointer' }}
                          />
                          <span>{col}</span>
                        </label>
                      );
                    })
                  )}
                </div>

                <div style={{ borderTop: '1px solid var(--border-color)', paddingTop: '16px' }}>
                  <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', flexWrap: 'wrap' }}>
                    <span style={{ fontSize: '0.85em', fontWeight: 600, color: 'var(--text-secondary)', width: '100%' }}>Presets:</span>
                    <button className="btn btn-secondary" style={{ padding: '4px 8px', fontSize: '0.8em', minWidth: 'auto' }} onClick={() => setHiddenColumns([])}>
                      Default (All)
                    </button>
                    <button className="btn btn-secondary" style={{ padding: '4px 8px', fontSize: '0.8em', minWidth: 'auto' }} onClick={() => setHiddenColumns(['Added Date', 'Updated Date', 'Updates', ...dynamicHeaders])}>
                      Minimal
                    </button>
                    <button className="btn btn-secondary" style={{ padding: '4px 8px', fontSize: '0.8em', minWidth: 'auto' }} onClick={() => setHiddenColumns(['Phone', 'First Name', 'Last Name', 'Added Date', 'Updated Date', 'Updates', ...dynamicHeaders])}>
                      Clear All
                    </button>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <button className="btn btn-primary" onClick={() => setShowDisplayColumnsModal(false)}>Close</button>
                  </div>
                </div>
              </div>
            </div>
          ), document.body)}

          {showSmartListModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ width: '500px', animation: 'fadeIn 0.2s ease-out' }}>
                <h3 className="card-title" style={{ marginBottom: '16px' }}>Create Smart List</h3>
                <div style={{ marginBottom: '12px' }}>
                  <label style={{ display: 'block', marginBottom: '4px', fontWeight: 500, fontSize: '0.85em' }}>List Name</label>
                  <input type="text" className="form-control" placeholder="e.g. VIP Guests" value={newSmartList.ListName} onChange={e => setNewSmartList({ ...newSmartList, ListName: e.target.value })} />
                </div>
                <div style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, fontSize: '0.85em' }}>Filters</label>
                  {newSmartList.Filters.map((f, i) => (
                    <div key={i} style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                      <select className="form-control" value={f.field} onChange={e => {
                        const newF = [...newSmartList.Filters];
                        newF[i].field = e.target.value;
                        setNewSmartList({ ...newSmartList, Filters: newF });
                      }} style={{ flex: 1 }}>
                        <option value="City">City</option>
                        <option value="State">State</option>
                        <option value="Country">Country</option>
                        <option value="CategoryId">Category ID</option>
                      </select>
                      <select className="form-control" value={f.operator} onChange={e => {
                        const newF = [...newSmartList.Filters];
                        newF[i].operator = e.target.value;
                        setNewSmartList({ ...newSmartList, Filters: newF });
                      }} style={{ width: '100px' }}>
                        <option value="equals">Equals</option>
                        <option value="contains">Contains</option>
                      </select>
                      <input type="text" className="form-control" placeholder="Value" value={f.value} onChange={e => {
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

          <div className="premium-table-card">
            <div className="premium-section-title">
              <Users size={20} style={{ color: 'var(--primary)' }} />
              Contacts Directory
            </div>
            
            {contacts.length === 0 ? (
              <div className="empty-state">No contacts found. Try importing a CSV!</div>
            ) : (
              <>
                <div className="premium-table-wrapper">
                  <table className="premium-table" style={{ margin: '0 24px', width: 'calc(100% - 48px)' }}>
                    <thead>
                      <tr>
                        {!hiddenColumns.includes('Phone') && <th>Phone</th>}
                        {!hiddenColumns.includes('Prefix') && <th>Prefix</th>}
                        {!hiddenColumns.includes('First Name') && <th>First Name</th>}
                        {!hiddenColumns.includes('Last Name') && <th>Last Name</th>}
                        {(Array.isArray(dynamicHeaders) ? dynamicHeaders : [])
                          .filter(h => h !== 'Prefix')
                          .filter(h => !hiddenColumns.includes(h))
                          .map(h => <th key={h}>{h}</th>)}
                        {!hiddenColumns.includes('Added Date') && <th>Added Date</th>}
                        {!hiddenColumns.includes('Updated Date') && <th>Updated Date</th>}
                        {!hiddenColumns.includes('Updates') && <th>Updates</th>}
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(Array.isArray(contacts) ? contacts : []).map(c => {
                        let fields = {};
                        try { fields = JSON.parse(c.CustomFieldsJson || c.customFieldsJson || '{}'); } catch(e){}
                        return (
                          <tr key={c.Id || c.id}>
                            {!hiddenColumns.includes('Phone') && <td style={{ fontFamily: 'monospace', fontWeight: 500 }}>{c.PhoneNumber || c.phoneNumber}</td>}
                            {!hiddenColumns.includes('Prefix') && <td>{fields['Prefix'] || '-'}</td>}
                            {!hiddenColumns.includes('First Name') && <td>{c.FirstName || c.firstName}</td>}
                            {!hiddenColumns.includes('Last Name') && <td>{c.LastName || c.lastName}</td>}
                            {(Array.isArray(dynamicHeaders) ? dynamicHeaders : [])
                              .filter(h => h !== 'Prefix')
                              .filter(h => !hiddenColumns.includes(h))
                              .map(h => <td key={h}>{fields[h] || '-'}</td>)}
                            {!hiddenColumns.includes('Added Date') && <td>{new Date(c.CreatedAt || c.createdAt).toLocaleDateString()}</td>}
                            {!hiddenColumns.includes('Updated Date') && <td>{c.UpdatedAt || c.updatedAt ? new Date(c.UpdatedAt || c.updatedAt).toLocaleDateString() : '-'}</td>}
                            {!hiddenColumns.includes('Updates') && (
                              <td>
                                <span className="badge badge-success">{c.UpdateCount || c.updateCount || 0}</span>
                              </td>
                            )}
                            <td>
                              <div className="premium-action-buttons">
                                <button className="premium-icon-btn edit" onClick={() => handleEditContactClick(c)} title="Edit">
                                  <Edit2 size={16} />
                                </button>
                                <button className="premium-icon-btn delete" onClick={() => handleDeleteContact(c.Id || c.id)} title="Delete">
                                  <Trash2 size={16} />
                                </button>
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                <div className="premium-pagination">
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <span>Rows per page:</span>
                    <select 
                      value={pageSize} 
                      onChange={(e) => { setPageSize(Number(e.target.value)); setCurrentPage(1); }}
                      style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-color)', outline: 'none' }}
                    >
                      <option value={10}>10</option>
                      <option value={20}>20</option>
                      <option value={50}>50</option>
                      <option value={100}>100</option>
                    </select>
                    <span style={{ marginLeft: '16px', fontWeight: 600, color: 'var(--text-primary)' }}>Total: {totalContacts}</span>
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
              </>
            )}
          </div>
          
          {showEditContactModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              {(() => {
                let fields = {};
                try { fields = JSON.parse(editContactData.CustomFieldsJson || '{}'); } catch(e) {}

                const handlePermanentFieldChange = (key, val) => {
                  const updatedFields = { ...fields, [key]: val };
                  setEditContactData({ ...editContactData, CustomFieldsJson: JSON.stringify(updatedFields) });
                };

                const dynamicKeys = Object.keys(fields).filter(k => !PERMANENT_CUSTOM_FIELDS.includes(k));

                return (
                  <div className="premium-analytics-modal" style={{ maxHeight: '90vh', display: 'flex', flexDirection: 'column', animation: 'fadeIn 0.2s ease-out', padding: 0, overflow: 'hidden' }}>
                    <div style={{ padding: '24px 24px 0 24px', flexShrink: 0 }}>
                      <h3 className="card-title" style={{ marginBottom: '16px' }}>{editingContactId ? 'Edit Contact' : 'Add Contact'}</h3>
                    </div>
                    
                    <div style={{ flex: 1, overflowY: 'auto', padding: '0 24px', minHeight: 0 }}>
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

                      {PERMANENT_CUSTOM_FIELDS.map(f => (
                        <div className="form-group" key={f} style={{ marginBottom: '16px' }}>
                          <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>{f}</label>
                          <input 
                            type="text" 
                            value={fields[f] || ''} 
                            onChange={e => handlePermanentFieldChange(f, e.target.value)} 
                            style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }} 
                          />
                        </div>
                      ))}

                      <div className="form-group" style={{ marginBottom: '16px', marginTop: '24px', borderTop: '1px solid #eee', paddingTop: '16px' }}>
                        <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500 }}>Custom Fields</label>
                        <div>
                          {dynamicKeys.map((k, i) => (
                            <div key={i} style={{ display: 'flex', gap: '8px', marginBottom: '8px' }}>
                              <input 
                                type="text" 
                                value={k} 
                                onChange={e => {
                                  const newKey = e.target.value;
                                  const newFields = {};
                                  Object.keys(fields).forEach(oldKey => {
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
                      </div>
                    </div>

                    <div style={{ padding: '16px 24px 24px 24px', display: 'flex', justifyContent: 'flex-end', gap: '12px', flexShrink: 0 }}>
                      <button className="btn btn-secondary" onClick={() => setShowEditContactModal(false)}>Cancel</button>
                      <button className="btn btn-primary" onClick={handleSaveContact}>Save</button>
                    </div>
                  </div>
                );
              })()}
            </div>
          ), document.body)}

          {importModalOpen && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ width: '800px', maxHeight: '90vh', overflowY: 'auto', animation: 'fadeIn 0.2s ease-out' }}>
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
            try {
              const errData = await res.json();
              const metaMsg = errData?.message || 'Failed to save template';
              alert(`Failed to save template:\n\n${metaMsg}`);
            } catch {
              alert('Failed to save template');
            }
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

      const handleSyncTemplates = async () => {
        setIsSyncing(true);
        try {
          const res = await fetch(`${API_BASE}/templates/sync`, { method: 'POST' });
          if (res.ok) {
            const data = await res.json();
            alert(`Sync successful! Added ${data.addedCount} new templates from Meta.`);
            fetchData();
          } else {
            let errorMsg = 'Failed to sync templates';
            try {
              const err = await res.json();
              if (err && err.message) errorMsg = err.message;
            } catch (e) {}
            alert(errorMsg);
          }
        } catch (e) {
          alert('Error syncing templates with Meta');
        } finally {
          setIsSyncing(false);
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
        <div className="premium-table-card" style={{ padding: '32px' }}>
          <div className="premium-toolbar">
            <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600, color: 'var(--text-primary)' }}>Message Templates</h3>
            <div style={{ display: 'flex', gap: '8px' }}>
              <button 
                className="btn btn-secondary" 
                onClick={handleSyncTemplates} 
                disabled={isSyncing}
                style={{ padding: '8px 16px', display: 'flex', alignItems: 'center', gap: '6px' }}
              >
                {isSyncing ? 'Syncing...' : 'Sync with Meta'}
              </button>
              <button className="btn btn-primary" onClick={() => {
                setNewTemplate({ Name: '', ExternalTemplateName: '', LanguageCode: 'en', ChannelType: 'WhatsApp', BodyParamCount: -1, Category: 'Utility', HeaderType: 'None', BodyText: '', ParameterDefinitionsJson: '[]' });
                setIsEditingTemplate(false);
                setEditingTemplateId(null);
                setShowTemplateModal(true);
              }}>
                <Plus size={16} /> Create Template
              </button>
            </div>
          </div>
          
          {showTemplateModal && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ width: '850px', maxWidth: '95vw', maxHeight: '90vh', display: 'flex', flexDirection: 'column', padding: '32px', overflow: 'hidden', animation: 'fadeIn 0.2s ease-out' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border-color, #eee)', paddingBottom: '12px', marginBottom: '16px' }}>
                  <h3 className="card-title" style={{ margin: 0 }}>{isEditingTemplate ? 'Edit Message Template' : 'New Message Template'}</h3>
                  <button className="btn btn-secondary" style={{ minWidth: 'auto', padding: '4px 8px', border: 'none' }} onClick={() => setShowTemplateModal(false)}>✕</button>
                </div>
                
                <div style={{ display: 'flex', gap: '24px', flex: 1, overflow: 'auto' }}>
                  {/* Left Column: Form & Parameter Table */}
                  <div style={{ flex: 1.3, display: 'flex', flexDirection: 'column', gap: '16px', overflowY: 'auto', paddingRight: '8px' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Display Name (e.g. CheckIn Welcome)</label>
                        <input type="text" className="form-control" placeholder="Display Name" value={newTemplate.Name} onChange={e => setNewTemplate({...newTemplate, Name: e.target.value})} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Meta Template Name</label>
                        <input type="text" className="form-control" placeholder="e.g. hotel_checkin" value={newTemplate.ExternalTemplateName} onChange={e => setNewTemplate({...newTemplate, ExternalTemplateName: e.target.value})} />
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '16px' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Language (e.g. en_US)</label>
                        <input type="text" className="form-control" placeholder="en" value={newTemplate.LanguageCode} onChange={e => setNewTemplate({...newTemplate, LanguageCode: e.target.value})} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Channel Type</label>
                        <select className="form-control" value={newTemplate.ChannelType} onChange={e => setNewTemplate({...newTemplate, ChannelType: e.target.value})} style={{ appearance: 'none', backgroundImage: 'url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2364748b\' stroke-width=\'2\' stroke-linecap=\'round\' stroke-linejoin=\'round\'%3e%3cpolyline points=\'6 9 12 15 18 9\'%3e%3c/polyline%3e%3c/svg%3e")', backgroundRepeat: 'no-repeat', backgroundPosition: 'right 12px center', backgroundSize: '16px', paddingRight: '36px' }}>
                          <option value="WhatsApp">WhatsApp</option>
                          <option value="SMS">SMS</option>
                        </select>
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Category</label>
                        <select className="form-control" value={newTemplate.Category || 'Utility'} onChange={e => setNewTemplate({...newTemplate, Category: e.target.value})} style={{ appearance: 'none', backgroundImage: 'url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2364748b\' stroke-width=\'2\' stroke-linecap=\'round\' stroke-linejoin=\'round\'%3e%3cpolyline points=\'6 9 12 15 18 9\'%3e%3c/polyline%3e%3c/svg%3e")', backgroundRepeat: 'no-repeat', backgroundPosition: 'right 12px center', backgroundSize: '16px', paddingRight: '36px' }}>
                          <option value="Utility">Utility (Transactional)</option>
                          <option value="Marketing">Marketing (Promotional)</option>
                        </select>
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '16px' }}>
                      <div>
                        <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Header Media Type</label>
                        <select className="form-control" value={newTemplate.HeaderType || 'None'} onChange={e => setNewTemplate({...newTemplate, HeaderType: e.target.value})} style={{ appearance: 'none', backgroundImage: 'url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2364748b\' stroke-width=\'2\' stroke-linecap=\'round\' stroke-linejoin=\'round\'%3e%3cpolyline points=\'6 9 12 15 18 9\'%3e%3c/polyline%3e%3c/svg%3e")', backgroundRepeat: 'no-repeat', backgroundPosition: 'right 12px center', backgroundSize: '16px', paddingRight: '36px' }}>
                          <option value="None">None</option>
                          <option value="Text">Text</option>
                          <option value="Document">Document (PDF Invoice generation)</option>
                          <option value="Image">Image</option>
                          <option value="Video">Video</option>
                        </select>
                      </div>
                      
                      {newTemplate.HeaderType && newTemplate.HeaderType !== 'None' && (
                        <div>
                          <label style={{ display: 'block', marginBottom: '6px', fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>
                            {newTemplate.HeaderType === 'Text' && 'Header Text'}
                            {newTemplate.HeaderType === 'Image' && 'Header Image URL'}
                            {newTemplate.HeaderType === 'Video' && 'Header Video URL'}
                            {newTemplate.HeaderType === 'Document' && 'Document URL (Optional - defaults to PDF invoice generator)'}
                          </label>
                          <input 
                            type="text" 
                            className="form-control" 
                            placeholder={
                              newTemplate.HeaderType === 'Text' ? "Enter header text (e.g. Booking Confirmed)" : "https://domain.com/media-file"
                            }
                            value={newTemplate.HeaderText || ''} 
                            onChange={e => setNewTemplate({...newTemplate, HeaderText: e.target.value})} 
                          />
                        </div>
                      )}
                    </div>

                    <div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                        <label style={{ fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Body Text</label>
                        <span style={{ fontSize: '0.8em', color: 'var(--text-secondary, #64748b)' }}>Use <code>{"{{1}}"}</code>, <code>{"{{2}}"}</code> for placeholders</span>
                      </div>
                      <textarea 
                        className="form-control" 
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
                        style={{ fontFamily: 'monospace', resize: 'vertical' }} 
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
                                        className="form-control"
                                        value={def.Label || def.label || ''} 
                                        onChange={e => {
                                          const val = e.target.value;
                                          const next = defs.map((d, i) => i === idx ? { ...d, Label: val } : d);
                                          setNewTemplate({ ...newTemplate, ParameterDefinitionsJson: JSON.stringify(next) });
                                        }}
                                        style={{ padding: '6px 10px', fontSize: '0.9em' }} 
                                        placeholder="e.g. GuestName"
                                      />
                                    </td>
                                    <td style={{ padding: '8px' }}>
                                      <input 
                                        type="text" 
                                        className="form-control"
                                        value={def.SampleValue || def.sampleValue || ''} 
                                        onChange={e => {
                                          const val = e.target.value;
                                          const next = defs.map((d, i) => i === idx ? { ...d, SampleValue: val } : d);
                                          setNewTemplate({ ...newTemplate, ParameterDefinitionsJson: JSON.stringify(next) });
                                        }}
                                        style={{ padding: '6px 10px', fontSize: '0.9em' }} 
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
                                        className="form-control"
                                        style={{ appearance: 'none', backgroundImage: 'url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2364748b\' stroke-width=\'2\' stroke-linecap=\'round\' stroke-linejoin=\'round\'%3e%3cpolyline points=\'6 9 12 15 18 9\'%3e%3c/polyline%3e%3c/svg%3e")', backgroundRepeat: 'no-repeat', backgroundPosition: 'right 8px center', backgroundSize: '12px', paddingRight: '24px', padding: '6px 10px', fontSize: '0.9em', marginBottom: (def.DynamicKey || def.dynamicKey) ? '4px' : '0' }}
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
                                            className="form-control"
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
                                            style={{ padding: '4px 8px', fontSize: '0.8em', borderColor: 'var(--primary)' }}
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
                      <label style={{ fontWeight: 600, fontSize: '0.85em', display: 'block', marginBottom: '6px', color: 'var(--text-secondary)' }}>Footer Text (Optional)</label>
                      <input 
                        type="text"
                        className="form-control"
                        placeholder="e.g. Reply STOP to unsubscribe"
                        value={newTemplate.FooterText || ''}
                        onChange={e => setNewTemplate({...newTemplate, FooterText: e.target.value})}
                      />
                    </div>

                    {/* Buttons Configuration */}
                    <div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                        <label style={{ fontWeight: 600, fontSize: '0.85em', color: 'var(--text-secondary)' }}>Buttons (Optional)</label>
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
                                  className="form-control"
                                  value={btn.type}
                                  onChange={e => {
                                    const next = [...btns];
                                    next[idx].type = e.target.value;
                                    if (e.target.value !== 'URL') delete next[idx].url;
                                    if (e.target.value !== 'PHONE_NUMBER') delete next[idx].phone_number;
                                    setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                  }}
                                  style={{ appearance: 'none', backgroundImage: 'url("data:image/svg+xml;charset=UTF-8,%3csvg xmlns=\'http://www.w3.org/2000/svg\' viewBox=\'0 0 24 24\' fill=\'none\' stroke=\'%2364748b\' stroke-width=\'2\' stroke-linecap=\'round\' stroke-linejoin=\'round\'%3e%3cpolyline points=\'6 9 12 15 18 9\'%3e%3c/polyline%3e%3c/svg%3e")', backgroundRepeat: 'no-repeat', backgroundPosition: 'right 8px center', backgroundSize: '12px', paddingRight: '24px', padding: '6px 10px', width: '150px', fontSize: '0.85em' }}
                                >
                                  <option value="QUICK_REPLY">Quick Reply</option>
                                  <option value="URL">Visit Website</option>
                                  <option value="PHONE_NUMBER">Call Number</option>
                                </select>
                                <input 
                                  type="text" 
                                  className="form-control"
                                  placeholder="Button Text (max 25 chars)"
                                  value={btn.text || ''}
                                  maxLength={25}
                                  onChange={e => {
                                    const next = [...btns];
                                    next[idx].text = e.target.value;
                                    setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                  }}
                                  style={{ padding: '6px 10px', flex: 1, fontSize: '0.85em' }}
                                />
                                {btn.type === 'URL' && (
                                  <input 
                                    type="text" 
                                    className="form-control"
                                    placeholder="URL (https://...)"
                                    value={btn.url || ''}
                                    onChange={e => {
                                      const next = [...btns];
                                      next[idx].url = e.target.value;
                                      setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                    }}
                                    style={{ padding: '6px 10px', flex: 1, fontSize: '0.85em' }}
                                  />
                                )}
                                {btn.type === 'PHONE_NUMBER' && (
                                  <input 
                                    type="text" 
                                    className="form-control"
                                    placeholder="+123456789"
                                    value={btn.phone_number || ''}
                                    onChange={e => {
                                      const next = [...btns];
                                      next[idx].phone_number = e.target.value;
                                      setNewTemplate({ ...newTemplate, ButtonsJson: JSON.stringify(next) });
                                    }}
                                    style={{ padding: '6px 10px', flex: 1, fontSize: '0.85em' }}
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
              <div className="premium-analytics-modal" style={{ width: '400px', animation: 'fadeIn 0.2s ease-out' }}>
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
              <thead><tr><th>Name</th><th>External Name</th><th>Channel</th><th>Status</th><th>Actions</th></tr></thead>
              <tbody>
                {(Array.isArray(templatesList) ? templatesList : []).map(t => (
                  <tr key={t.Id || t.id}>
                    <td>{t.Name || t.name}</td>
                    <td>{t.ExternalTemplateName || t.externalTemplateName || '-'}</td>
                    <td><span className="badge badge-sent">{t.ChannelType || t.channelType}</span></td>
                    <td>
                      {(() => {
                        const status = (t.Status || t.status || 'APPROVED').toUpperCase();
                        let badgeStyle = { backgroundColor: 'var(--surface-color)', color: 'var(--text-secondary)' };
                        if (status === 'APPROVED') {
                          badgeStyle = { backgroundColor: '#dcfce7', color: '#15803d' };
                        } else if (status === 'PENDING') {
                          badgeStyle = { backgroundColor: '#fef9c3', color: '#a16207' };
                        } else if (status === 'REJECTED' || status === 'PAUSED') {
                          badgeStyle = { backgroundColor: '#fee2e2', color: '#b91c1c' };
                        }
                        return <span className="badge" style={{ ...badgeStyle, padding: '4px 8px', borderRadius: '4px', fontSize: '0.75em', fontWeight: 600 }}>{status}</span>;
                      })()}
                    </td>
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
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)', overflowY: 'auto', padding: '20px' }}>
              <div className="premium-analytics-modal" style={{ width: '600px', maxHeight: '90vh', overflowY: 'auto', animation: 'fadeIn 0.2s ease-out' }}>
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
        <div className="premium-table-card">
          <div className="premium-toolbar">
            <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600, color: 'var(--text-primary)' }}>Automation Rules</h3>
            <button className="btn btn-primary" onClick={() => { setEditingRule(null); setIsBuildingRule(true); }} style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Plus size={16} /> Create Rule
            </button>
          </div>
          <div className="premium-table-wrapper">
            <table className="premium-table" style={{ margin: '0 24px', width: 'calc(100% - 48px)' }}>
              <thead><tr><th>Name & Trigger</th><th>Action Template</th><th>Stats</th><th>Status</th><th>Actions</th></tr></thead>
              <tbody>
                {(Array.isArray(rulesList) ? rulesList : []).map(r => (
                  <tr key={r.Id || r.id} style={{ opacity: (r.IsEnabled ?? r.isEnabled) ? 1 : 0.6 }}>
                    <td>
                      <div style={{ fontWeight: 600, color: 'var(--text-primary)', marginBottom: '4px' }}>{r.Name || r.name}</div>
                      <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', marginBottom: '8px' }}>{r.Description || r.description}</div>
                      <span className="badge badge-pending">{r.TriggerEventType || r.triggerEventType}</span>
                    </td>
                    <td><span className="badge badge-delivered">Template ID: {r.TemplateId || r.templateId}</span></td>
                    <td style={{ fontSize: '0.85rem' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                        <div style={{ display: 'flex', gap: '12px' }}>
                          <span title="Triggered" style={{ color: '#0d9488', fontWeight: 500 }}>T: {r.stats?.Triggered ?? r.stats?.triggered ?? 0}</span>
                          <span title="Sent" style={{ color: '#2563eb', fontWeight: 500 }}>S: {r.stats?.TotalSent ?? r.stats?.totalSent ?? 0}</span>
                          <span title="Failed" style={{ color: '#dc2626', fontWeight: 500 }}>F: {r.stats?.Failed ?? r.stats?.failed ?? 0}</span>
                        </div>
                        <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                          {r.stats?.LastRun || r.stats?.lastRun ? `Last run: ${new Date(r.stats?.LastRun || r.stats?.lastRun).toLocaleString([], {month:'short', day:'numeric', hour:'2-digit', minute:'2-digit'})}` : 'Never run'}
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className={`badge ${(r.IsEnabled ?? r.isEnabled) ? 'badge-delivered' : 'badge-failed'}`}>
                        {(r.IsEnabled ?? r.isEnabled) ? 'Active' : 'Paused'}
                      </span>
                    </td>
                     <td>
                      <div className="premium-action-buttons">
                        <button className="premium-icon-btn edit" onClick={() => handleEditRule(r)} title="Edit">
                          <Edit2 size={18} />
                        </button>
                        <button className="premium-icon-btn edit" style={{ color: (r.IsEnabled ?? r.isEnabled) ? '#f59e0b' : 'var(--success)', borderColor: 'transparent' }} onClick={() => handleToggleRule(r.Id || r.id, (r.IsEnabled ?? r.isEnabled))} title={(r.IsEnabled ?? r.isEnabled) ? "Pause" : "Resume"}>
                          {(r.IsEnabled ?? r.isEnabled) ? <XCircle size={18} /> : <CheckCircle2 size={18} />}
                        </button>
                        <button className="premium-icon-btn edit" style={{ color: 'var(--primary)', borderColor: 'transparent' }} onClick={() => handleShowAnalytics(r)} title="Analytics">
                          <BarChart2 size={18} />
                        </button>
                        <button className="premium-icon-btn delete" onClick={() => handleDeleteRule(r.Id || r.id)} title="Delete">
                          <Trash2 size={18} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {rulesList.length === 0 && (
                  <tr>
                    <td colSpan="5" style={{ textAlign: 'center', padding: '40px' }}>
                      <div className="empty-state">No automation rules configured yet. Create one to get started!</div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {analyticsRule && createPortal((
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ animation: 'fadeIn 0.2s ease-out' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
                  <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600, color: 'var(--text-primary)' }}>Rule Analytics</h3>
                  <button className="premium-icon-btn" onClick={() => setAnalyticsRule(null)}>✕</button>
                </div>
                
                <p style={{ color: 'var(--text-secondary)', marginBottom: '20px', fontSize: '0.95rem' }}>Viewing stats for: <strong>{analyticsRule.Name || analyticsRule.name}</strong></p>
                
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '32px' }}>
                  <div className="premium-stat-card premium-stat-triggered">
                    <div style={{ fontSize: '0.8rem', color: '#0d9488', fontWeight: 600, letterSpacing: '0.05em', marginBottom: '8px' }}>TRIGGERED</div>
                    <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#0f766e' }}>
                      {analyticsRule.stats?.Triggered ?? analyticsRule.stats?.triggered ?? 0}
                    </div>
                  </div>
                  <div className="premium-stat-card premium-stat-sent">
                    <div style={{ fontSize: '0.8rem', color: '#2563eb', fontWeight: 600, letterSpacing: '0.05em', marginBottom: '8px' }}>SENT SUCCESSFULLY</div>
                    <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#1d4ed8' }}>
                      {analyticsRule.stats?.TotalSent ?? analyticsRule.stats?.totalSent ?? 0}
                    </div>
                  </div>
                  <div className="premium-stat-card premium-stat-failed">
                    <div style={{ fontSize: '0.8rem', color: '#dc2626', fontWeight: 600, letterSpacing: '0.05em', marginBottom: '8px' }}>FAILED</div>
                    <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#b91c1c' }}>
                      {analyticsRule.stats?.Failed ?? analyticsRule.stats?.failed ?? 0}
                    </div>
                  </div>
                </div>

                <h4 style={{ marginBottom: '12px', color: 'var(--text-primary)', fontWeight: 600, fontSize: '1rem' }}>Recent Failure Log</h4>
                <div style={{ flex: 1, overflowY: 'auto', maxHeight: '300px', border: '1px solid var(--border-color)', borderRadius: '12px', backgroundColor: '#f8fafc', padding: '16px' }}>
                  {ruleFailures.length === 0 ? (
                    <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '32px 0', fontSize: '0.95rem' }}>
                      No failed messages recorded for this rule.
                    </div>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                      {ruleFailures.map((f, i) => (
                        <div key={i} style={{ padding: '12px', backgroundColor: '#ffffff', border: '1px solid var(--border-color)', borderRadius: '8px', display: 'flex', flexDirection: 'column', gap: '8px', boxShadow: '0 1px 2px rgba(0,0,0,0.02)' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.9rem' }}>
                            <span style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{f.ToPhoneNumber || f.toPhoneNumber}</span>
                            <span style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>{new Date(f.CreatedAt || f.createdAt).toLocaleString()}</span>
                          </div>
                          <div style={{ fontSize: '0.85rem', color: '#b91c1c', backgroundColor: '#fef2f2', padding: '8px 12px', borderRadius: '6px', border: '1px solid #fecaca' }}>
                            <strong>Reason:</strong> {f.FailureReason || f.failureReason || 'Failed to connect to WhatsApp API.'}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
                
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '24px' }}>
                  <button className="btn btn-primary" onClick={() => setAnalyticsRule(null)}>Done</button>
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
          <div className="premium-toolbar" style={{ marginBottom: '24px' }}>
            <h2 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 'bold', color: 'var(--text-primary)' }}>Analytics Overview</h2>
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ fontSize: '0.9rem', color: 'var(--text-secondary)', fontWeight: 500 }}>Date Range:</span>
                <select 
                  className="premium-chat-input" 
                  style={{ padding: '8px 16px', width: '200px', backgroundColor: '#fff', border: '1px solid var(--border-color)', borderRadius: '8px' }}
                  value={analyticsPeriod}
                  onChange={(e) => setAnalyticsPeriod(e.target.value)}
                >
                  <option value="today">Today</option>
                  <option value="7">Last 7 Days</option>
                  <option value="30">Last 30 Days</option>
                  <option value="all">All Time</option>
                </select>
              </div>
              <button className="btn btn-primary" onClick={() => window.location.href = `${API_BASE}/analytics/export`} style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <Download size={16} /> Export CSV
              </button>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginBottom: '32px' }}>
            <div className="premium-stat-card premium-stat-triggered">
              <div style={{ color: '#0d9488', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600, letterSpacing: '0.05em' }}>OVERALL TRIGGERED</div>
              <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: '#0f766e' }}>{overallTriggered}</div>
            </div>
            <div className="premium-stat-card premium-stat-sent">
              <div style={{ color: '#2563eb', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600, letterSpacing: '0.05em' }}>SUCCESSFULLY SENT</div>
              <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: '#1d4ed8' }}>{overallSent}</div>
            </div>
            <div className="premium-stat-card premium-stat-failed">
              <div style={{ color: '#dc2626', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600, letterSpacing: '0.05em' }}>FAILED DELIVERIES</div>
              <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: '#b91c1c' }}>{overallFailed}</div>
            </div>
            <div className="premium-stat-card" style={{ background: 'linear-gradient(135deg, rgba(234, 179, 8, 0.1) 0%, rgba(202, 138, 4, 0.05) 100%)', border: '1px solid rgba(234, 179, 8, 0.2)' }}>
              <div style={{ color: '#ca8a04', marginBottom: '8px', fontSize: '0.85rem', fontWeight: 600, letterSpacing: '0.05em' }}>DELIVERY SUCCESS RATE</div>
              <div style={{ fontSize: '2.5rem', fontWeight: 'bold', color: '#a16207' }}>{successRate}%</div>
            </div>
          </div>

          <div className="premium-table-card" style={{ marginBottom: '32px' }}>
            <h3 className="card-title" style={{ padding: '20px 24px', margin: 0, borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Workflow size={20} color="var(--primary)" /> Automation Performance
            </h3>
            <div className="premium-table-wrapper">
              <table className="premium-table" style={{ margin: '0 24px', width: 'calc(100% - 48px)' }}>
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
                        <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{r.Name || r.name}</td>
                        <td><span className="badge badge-pending">{r.TriggerEventType || r.triggerEventType}</span></td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{trig}</td>
                        <td style={{ textAlign: 'center', color: '#10b981', fontWeight: 600 }}>{s}</td>
                        <td style={{ textAlign: 'center', color: '#ef4444', fontWeight: 600 }}>{f}</td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{ruleRate}%</td>
                        <td style={{ textAlign: 'center' }}>
                          <button 
                            className="btn btn-secondary" 
                            style={{ padding: '6px 12px', fontSize: '0.8rem', display: 'inline-flex', alignItems: 'center', gap: '6px', background: 'transparent', border: '1px solid var(--primary)', color: 'var(--primary)' }}
                            onClick={() => handleShowAnalytics(r)}
                          >
                            <BarChart2 size={14} /> Details
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                  {currentRulesPage.length === 0 && (
                    <tr>
                      <td colSpan="7" style={{ textAlign: 'center', padding: '32px' }}>
                        <div className="empty-state">No automation rules found.</div>
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            
            <div className="premium-pagination">
              <div style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>Total: {rulesSafe.length}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <button 
                  className="btn btn-secondary" 
                  disabled={analyticsRulesPage === 1} 
                  onClick={() => setAnalyticsRulesPage(p => Math.max(1, p - 1))}
                  style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                >
                  Previous
                </button>
                <span style={{ fontWeight: 500, fontSize: '0.9rem', color: 'var(--text-primary)' }}>Page {analyticsRulesPage} of {Math.ceil(rulesSafe.length / analyticsPageSize) || 1}</span>
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

          <div className="premium-table-card">
            <h3 className="card-title" style={{ padding: '20px 24px', margin: 0, borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Megaphone size={20} color="var(--primary)" /> Broadcast Campaigns Performance
            </h3>
            <div className="premium-table-wrapper">
              <table className="premium-table" style={{ margin: '0 24px', width: 'calc(100% - 48px)' }}>
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
                        <td style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>{new Date(b.StartedAt || b.startedAt).toLocaleString()}</td>
                        <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{b.TemplateName || b.templateName}</td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{total}</td>
                        <td style={{ textAlign: 'center', color: '#10b981', fontWeight: 600 }}>{s}</td>
                        <td style={{ textAlign: 'center', color: '#ef4444', fontWeight: 600 }}>{f}</td>
                        <td style={{ textAlign: 'center', color: '#eab308', fontWeight: 600 }}>{p}</td>
                        <td style={{ textAlign: 'center', fontWeight: 600 }}>{rate}%</td>
                        <td style={{ textAlign: 'center' }}>
                          <button 
                            className="btn btn-secondary" 
                            style={{ padding: '6px 12px', fontSize: '0.8rem', display: 'inline-flex', alignItems: 'center', gap: '6px', background: 'transparent', border: '1px solid var(--primary)', color: 'var(--primary)' }}
                            onClick={() => handleShowBroadcastAnalytics(b)}
                          >
                            <BarChart2 size={14} /> Details
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                  {currentBroadcastsPage.length === 0 && (
                    <tr>
                      <td colSpan="8" style={{ textAlign: 'center', padding: '32px' }}>
                        <div className="empty-state">No broadcast campaigns found.</div>
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <div className="premium-pagination">
              <div style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>Total: {broadcastsSafe.length}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                <button 
                  className="btn btn-secondary" 
                  disabled={analyticsBroadcastsPage === 1} 
                  onClick={() => setAnalyticsBroadcastsPage(p => Math.max(1, p - 1))}
                  style={{ padding: '6px 12px', fontSize: '0.85rem' }}
                >
                  Previous
                </button>
                <span style={{ fontWeight: 500, fontSize: '0.9rem', color: 'var(--text-primary)' }}>Page {analyticsBroadcastsPage} of {Math.ceil(broadcastsSafe.length / analyticsPageSize) || 1}</span>
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
            <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
              <div className="premium-analytics-modal" style={{ animation: 'fadeIn 0.2s ease-out' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
                  <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600, color: 'var(--text-primary)' }}>Analytics: {analyticsRule.Name || analyticsRule.name}</h3>
                  <button className="premium-icon-btn" onClick={() => setAnalyticsRule(null)}>✕</button>
                </div>
                
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '32px' }}>
                  <div className="premium-stat-card premium-stat-triggered">
                    <div style={{ fontSize: '0.8rem', color: '#0d9488', fontWeight: 600, letterSpacing: '0.05em', marginBottom: '8px' }}>TRIGGERED</div>
                    <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#0f766e' }}>
                      {analyticsRule.stats?.Triggered ?? analyticsRule.stats?.triggered ?? 0}
                    </div>
                  </div>
                  <div className="premium-stat-card premium-stat-sent">
                    <div style={{ fontSize: '0.8rem', color: '#2563eb', fontWeight: 600, letterSpacing: '0.05em', marginBottom: '8px' }}>SENT SUCCESSFULLY</div>
                    <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#1d4ed8' }}>
                      {analyticsRule.stats?.TotalSent ?? analyticsRule.stats?.totalSent ?? 0}
                    </div>
                  </div>
                  <div className="premium-stat-card premium-stat-failed">
                    <div style={{ fontSize: '0.8rem', color: '#dc2626', fontWeight: 600, letterSpacing: '0.05em', marginBottom: '8px' }}>FAILED</div>
                    <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#b91c1c' }}>
                      {analyticsRule.stats?.Failed ?? analyticsRule.stats?.failed ?? 0}
                    </div>
                  </div>
                </div>

                <h4 style={{ marginBottom: '12px', color: 'var(--text-primary)', fontWeight: 600, fontSize: '1rem' }}>Delivery Details Log</h4>
                <div style={{ flex: 1, overflowY: 'auto', maxHeight: '350px', border: '1px solid var(--border-color)', borderRadius: '12px', backgroundColor: '#f8fafc', padding: '16px' }}>
                  {ruleFailures.length === 0 ? (
                    <div style={{ textAlign: 'center', color: 'var(--text-secondary)', padding: '32px 0', fontSize: '0.95rem' }}>
                      No messages recorded.
                    </div>
                  ) : (
                    <table className="premium-table" style={{ margin: 0, backgroundColor: 'white', borderRadius: '8px', overflow: 'hidden', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
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
                            <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>
                              {f.ToPhoneNumber || f.toPhoneNumber}
                              {(f.IsUnsubscribed || f.isUnsubscribed) === 1 && (
                                <span style={{ marginLeft: '8px', fontSize: '10px', backgroundColor: '#ef4444', color: 'white', padding: '2px 6px', borderRadius: '4px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Unsubscribed</span>
                              )}
                            </td>
                            <td>
                              <span className={`badge badge-${(f.Status || f.status || '').toLowerCase()}`}>
                                {f.Status || f.status}
                              </span>
                            </td>
                            <td style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>
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
                
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '24px' }}>
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
        <div className="premium-table-card">
          <div className="premium-toolbar">
            <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <UserX size={20} color="var(--primary)" /> Marketing Opt-Outs (Unsubscribed)
            </h3>
          </div>
          <div style={{ padding: '0 24px 16px 24px' }}>
            <p style={{ color: 'var(--text-secondary)', margin: 0, fontSize: '0.95rem' }}>
              Users who have replied "STOP" or opted out of "Offers and announcements" on WhatsApp. They will not receive marketing templates.
            </p>
          </div>
          <div className="premium-table-wrapper">
            <table className="premium-table" style={{ margin: '0 24px', width: 'calc(100% - 48px)' }}>
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
                    <td style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{o.PhoneNumber || o.phoneNumber}</td>
                    <td>{o.FirstName || o.firstName ? `${o.FirstName || o.firstName} ${o.LastName && o.LastName !== 'undefined' ? o.LastName : (o.lastName && o.lastName !== 'undefined' ? o.lastName : '')}`.trim() : '-'}</td>
                    <td style={{ color: 'var(--text-secondary)' }}>{new Date(o.UnsubscribedAt || o.unsubscribedAt).toLocaleString()}</td>
                    <td><span className="badge badge-pending">{o.Source || o.source}</span></td>
                    <td style={{ textAlign: 'center' }}>
                      <button 
                        className="btn btn-secondary" 
                        style={{ padding: '6px 12px', fontSize: '0.8rem', display: 'inline-flex', alignItems: 'center', gap: '6px', background: 'transparent', border: '1px solid #10b981', color: '#10b981' }}
                        onClick={() => handleResubscribe(o.PhoneNumber || o.phoneNumber)}
                        title="Re-Subscribe User"
                      >
                        <UserPlus size={14} /> Re-Subscribe
                      </button>
                    </td>
                  </tr>
                ))}
                {(optOutsList || []).length === 0 && (
                  <tr>
                    <td colSpan="5" style={{ textAlign: 'center', padding: '32px' }}>
                      <div className="empty-state">No unsubscribed contacts found.</div>
                    </td>
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
        <div className="premium-dashboard-container">
          <div className="premium-campaign-card">
            <h3 className="card-title" style={{ fontSize: '1.25rem', marginBottom: '8px' }}>New Broadcast Campaign</h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.9rem' }}>Select an audience and a template to launch a new messaging campaign.</p>
            
            <div className="premium-campaign-form">
              <div className="form-group">
                <label>Target Audience</label>
                <select 
                  value={selectedBroadcastCategory}
                  onChange={e => setSelectedBroadcastCategory(e.target.value)}
                >
                  <option value="">All Contacts</option>
                  {(Array.isArray(categories) ? categories : []).map(c => <option key={c.Id || c.id} value={c.Id || c.id}>{c.Name || c.name}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label>Message Template</label>
                <select 
                  value={selectedBroadcastTemplate}
                  onChange={e => setSelectedBroadcastTemplate(e.target.value)}
                >
                  <option value="">Select a template...</option>
                  {(Array.isArray(templatesList) ? templatesList : []).map(t => <option key={t.Id || t.id} value={t.Id || t.id}>{t.Name || t.name}</option>)}
                </select>
              </div>
              <div>
                <button className="btn btn-primary" onClick={handleSendBroadcast} style={{ padding: '12px 24px', borderRadius: '8px', fontSize: '0.95rem' }}>
                  <Megaphone size={18} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
                  Send Broadcast
                </button>
              </div>
            </div>
          </div>

          <div className="premium-table-card">
            <div className="premium-section-title">
              <Activity size={20} style={{ color: 'var(--primary)' }} />
              Recent Broadcasts
            </div>
            
            {(broadcasts || []).length === 0 ? (
              <div className="empty-state">No broadcasts sent yet. Start a new campaign above!</div>
            ) : (
              <>
                <div className="premium-table-wrapper">
                  <table className="premium-table" style={{ margin: '0 24px', width: 'calc(100% - 48px)' }}>
                    <thead>
                      <tr>
                        <th>Started At</th>
                        <th>Template</th>
                        <th>Total Queued</th>
                        <th>Delivered</th>
                        <th>Pending</th>
                        <th>Failed</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {(Array.isArray(broadcasts) ? broadcasts : []).map((b, i) => (
                        <tr key={i}>
                          <td style={{ fontWeight: 500 }}>{new Date(b.StartedAt || b.startedAt).toLocaleString()}</td>
                          <td>{b.TemplateName || b.templateName}</td>
                          <td style={{ fontFamily: 'monospace', fontSize: '1.05em' }}>{b.TotalQueued ?? b.totalQueued ?? b.TotalSent ?? b.totalSent ?? 0}</td>
                          <td><span className="badge badge-delivered">{b.Delivered ?? b.delivered ?? 0}</span></td>
                          <td><span className="badge badge-pending">{b.Pending ?? b.pending ?? 0}</span></td>
                          <td><span className="badge badge-failed">{b.Failed ?? b.failed ?? 0}</span></td>
                          <td>
                            <div className="premium-action-buttons">
                              {(b.Pending || b.pending) > 0 && (
                                <button 
                                  className="premium-icon-btn warning" 
                                  onClick={() => handleStopBroadcast(b)}
                                  title="Stop pending messages"
                                >
                                  <XCircle size={18} />
                                </button>
                              )}
                              <button 
                                className="premium-icon-btn delete" 
                                onClick={() => handleDeleteBroadcast(b)}
                                title="Delete from history"
                              >
                                <Trash2 size={18} />
                              </button>
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div className="premium-pagination">
                  <div>
                    <span style={{ color: 'var(--text-secondary)' }}>Showing {broadcasts.length} campaigns</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
                    <button 
                      className="btn btn-secondary" 
                      disabled={bPage === 1} 
                      onClick={() => setBPage(p => p - 1)}
                    >
                      Previous
                    </button>
                    <span style={{ fontWeight: 500, color: 'var(--text-primary)' }}>
                      Page {bPage} of {Math.ceil(bTotal / bLimit) || 1}
                    </span>
                    <button 
                      className="btn btn-secondary" 
                      disabled={bPage >= Math.ceil(bTotal / bLimit) || bTotal === 0} 
                      onClick={() => setBPage(p => p + 1)}
                    >
                      Next
                    </button>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>
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

          <div style={{ marginBottom: '32px' }}>
            <div className="card">
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

    if (activeTab === 'users') {
      const modulesConfig = [
        { key: 'dashboard', name: 'Dashboard' },
        { key: 'contacts', name: 'Contacts Directory' },
        { key: 'templates', name: 'Message Templates' },
        { key: 'broadcasts', name: 'Broadcasts' },
        { key: 'rules', name: 'Automation & Rules' },
        { key: 'analytics', name: 'Analytics & Reports' },
        { key: 'optouts', name: 'Opt-Outs List' },
        { key: 'manual-checkin', name: 'Manual Check-In' },
        { key: 'manual-checkout', name: 'Manual Check-Out' }
      ];
      const targetRoles = ['Manager', 'Employer'];

      return (
        <div style={{ padding: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
            <h2 style={{ margin: 0, fontWeight: 700, color: 'var(--text-primary)' }}>User Accounts & Access Control</h2>
            {(user?.role === 'Owner' || user?.role === 'Developer') && userSubTab === 'list' && (
              <button className="btn btn-primary" style={{ display: 'flex', alignItems: 'center', gap: '8px' }} onClick={() => setShowCreateUserModal(true)}>
                <UserPlus size={18} /> Add User Account
              </button>
            )}
          </div>

          <div style={{ display: 'flex', gap: '16px', borderBottom: '1px solid var(--border-color)', marginBottom: '24px' }}>
            <button 
              onClick={() => setUserSubTab('list')}
              style={{
                padding: '10px 16px',
                border: 'none',
                background: 'none',
                fontSize: '15px',
                fontWeight: 600,
                color: userSubTab === 'list' ? 'var(--primary)' : 'var(--text-secondary)',
                borderBottom: userSubTab === 'list' ? '2px solid var(--primary)' : '2px solid transparent',
                cursor: 'pointer'
              }}
            >
              User Accounts List
            </button>
            <button 
              onClick={() => setUserSubTab('permissions')}
              style={{
                padding: '10px 16px',
                border: 'none',
                background: 'none',
                fontSize: '15px',
                fontWeight: 600,
                color: userSubTab === 'permissions' ? 'var(--primary)' : 'var(--text-secondary)',
                borderBottom: userSubTab === 'permissions' ? '2px solid var(--primary)' : '2px solid transparent',
                cursor: 'pointer'
              }}
            >
              Role Permissions Access
            </button>
          </div>

          {userSubTab === 'list' ? (
            <div className="premium-card" style={{ padding: 0, overflow: 'hidden' }}>
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', minWidth: '950px' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border-color)', backgroundColor: 'rgba(0,0,0,0.02)' }}>
                    <th style={{ padding: '16px 24px', fontWeight: 600 }}>Username</th>
                    <th style={{ padding: '16px 24px', fontWeight: 600 }}>Role</th>
                    <th style={{ padding: '16px 24px', fontWeight: 600 }}>Signature / Stamp</th>
                    <th style={{ padding: '16px 24px', fontWeight: 600 }}>Created At</th>
                    <th style={{ padding: '16px 24px', fontWeight: 600 }}>Status</th>
                    {(user?.role === 'Owner' || user?.role === 'Developer') && <th style={{ padding: '16px 24px', fontWeight: 600 }}>Action</th>}
                  </tr>
                </thead>
                <tbody>
                {usersList.map(u => {
                  const id = u.id ?? u.Id;
                  const username = u.username ?? u.Username;
                  const role = u.role ?? u.Role;
                  const isActive = u.isActive ?? u.IsActive;
                  const createdAt = u.createdAt ?? u.CreatedAt;

                  return (
                    <tr key={id} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '16px 24px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                          <div style={{ width: '32px', height: '32px', borderRadius: '50%', backgroundColor: 'var(--primary)', color: 'white', display: 'flex', justifyContent: 'center', alignItems: 'center', fontWeight: 600 }}>
                            {(username || '').charAt(0).toUpperCase()}
                          </div>
                          <strong>{username}</strong>
                        </div>
                      </td>
                      <td style={{ padding: '16px 24px' }}>
                        {(user?.role === 'Owner' || user?.role === 'Developer') && username !== user?.username ? (
                          <select
                            value={role}
                            onChange={(e) => handleUpdateUserRole(id, e.target.value)}
                            style={{
                              padding: '6px 12px',
                              borderRadius: '6px',
                              border: '1px solid var(--border-color)',
                              backgroundColor: 'var(--surface-color)',
                              color: 'var(--text-primary)',
                              fontSize: '13px',
                              fontWeight: 600,
                              cursor: 'pointer'
                            }}
                          >
                            <option value="Owner">Owner</option>
                            <option value="Developer">Developer</option>
                            <option value="Manager">Manager</option>
                            <option value="Employer">Employer</option>
                          </select>
                        ) : (
                          <span style={{ 
                            padding: '4px 10px', 
                            borderRadius: '12px', 
                            fontSize: '12px', 
                            fontWeight: 600,
                            backgroundColor: role === 'Owner' ? 'rgba(59, 130, 246, 0.15)' : role === 'Developer' ? 'rgba(16, 185, 129, 0.15)' : role === 'Manager' ? 'rgba(245, 158, 11, 0.15)' : 'rgba(100, 116, 139, 0.15)',
                            color: role === 'Owner' ? '#2563eb' : role === 'Developer' ? '#059669' : role === 'Manager' ? '#d97706' : '#475569'
                          }}>
                            {role}
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '16px 24px' }}>
                        {u.signaturePath || u.SignaturePath ? (
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <img 
                              src={`${API_BASE.replace('/api', '')}${u.signaturePath || u.SignaturePath}`} 
                              alt="Signature" 
                              style={{ height: '36px', maxHeight: '36px', width: 'auto', borderRadius: '4px', border: '1px solid var(--border-color)', padding: '2px', backgroundColor: 'white' }} 
                            />
                            {(user?.role === 'Owner' || user?.role === 'Developer') && (
                              <label style={{ cursor: 'pointer', color: 'var(--primary)', fontSize: '12px', fontWeight: 600 }}>
                                Change
                                <input 
                                  type="file" 
                                  accept="image/*"
                                  onChange={(e) => handleUploadSignature(id, e.target.files[0])}
                                  style={{ display: 'none' }}
                                />
                              </label>
                            )}
                          </div>
                        ) : (
                          <div>
                            {(user?.role === 'Owner' || user?.role === 'Developer') ? (
                              <label className="btn btn-secondary" style={{ padding: '4px 10px', fontSize: '12px', cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                                <Upload size={14} /> Upload Stamp/Sig
                                <input 
                                  type="file" 
                                  accept="image/*"
                                  onChange={(e) => handleUploadSignature(id, e.target.files[0])}
                                  style={{ display: 'none' }}
                                />
                              </label>
                            ) : (
                              <span style={{ fontSize: '13px', color: 'var(--text-secondary)', fontStyle: 'italic' }}>No signature</span>
                            )}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '16px 24px', color: 'var(--text-secondary)' }}>
                        {createdAt ? new Date(createdAt).toLocaleDateString() : ''}
                      </td>
                      <td style={{ padding: '16px 24px' }}>
                        <span style={{ 
                          display: 'inline-flex', 
                          alignItems: 'center', 
                          gap: '6px',
                          color: isActive ? 'var(--success)' : 'var(--danger)', 
                          fontWeight: 600 
                        }}>
                          <span style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: isActive ? 'var(--success)' : 'var(--danger)' }}></span>
                          {isActive ? 'Active' : 'Disabled'}
                        </span>
                      </td>
                      {/* Actions column for Owner or Developer */}
                      {(user?.role === 'Owner' || user?.role === 'Developer') && (
                        <td style={{ padding: '16px 24px' }}>
                          {username !== user?.username ? (
                            <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                              <button
                                className="btn btn-secondary"
                                style={{ 
                                  padding: '6px 12px', 
                                  fontSize: '13px', 
                                  borderColor: isActive ? 'var(--danger)' : 'var(--primary)',
                                  color: isActive ? 'var(--danger)' : 'var(--primary)'
                                }}
                                onClick={() => handleToggleUserStatus(id, !isActive)}
                              >
                                {isActive ? 'Disable' : 'Enable'}
                              </button>
                              <button
                                className="btn btn-secondary"
                                style={{ padding: '6px 12px', fontSize: '13px' }}
                                onClick={() => {
                                  setResetPasswordUserId(id);
                                  setResetPasswordUsername(username);
                                  setShowResetPasswordModal(true);
                                }}
                              >
                                Reset PW
                              </button>
                              <button
                                className="btn btn-secondary"
                                style={{ padding: '6px 12px', fontSize: '13px', color: 'var(--danger)', borderColor: 'rgba(239,68,68,0.2)' }}
                                onClick={() => handleDeleteUser(id, username)}
                              >
                                Delete
                              </button>
                              {isActive && (
                                <button
                                  className="btn btn-secondary"
                                  style={{ padding: '6px 12px', fontSize: '13px', color: 'var(--primary)', borderColor: 'rgba(59,130,246,0.2)' }}
                                  onClick={() => handleImpersonateUser(id)}
                                >
                                  Impersonate
                                </button>
                              )}
                            </div>
                          ) : (
                            <span style={{ color: 'var(--text-secondary)', fontSize: '13px', fontStyle: 'italic' }}>Active User</span>
                          )}
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
          ) : (
            <div className="premium-card" style={{ padding: '24px', overflow: 'hidden' }}>
              <p style={{ color: 'var(--text-secondary)', fontSize: '14px', marginBottom: '20px' }}>
                Toggle which sections/modules are visible in the sidebar menu for Manager and Employer roles. (Owners and Developers always have 100% access).
              </p>
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', minWidth: '600px' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border-color)', backgroundColor: 'rgba(0,0,0,0.02)' }}>
                    <th style={{ padding: '16px 24px', fontWeight: 600 }}>Module / Page</th>
                    {targetRoles.map(role => (
                      <th key={role} style={{ padding: '16px 24px', fontWeight: 600, textAlign: 'center' }}>
                        {role} Access
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {modulesConfig.map(mod => (
                    <tr key={mod.key} style={{ borderBottom: '1px solid var(--border-color)' }}>
                      <td style={{ padding: '16px 24px', fontWeight: 600 }}>{mod.name}</td>
                      {targetRoles.map(role => {
                        const match = permissions.find(p => p.role.toLowerCase() === role.toLowerCase() && p.module.toLowerCase() === mod.key.toLowerCase());
                        const isChecked = match ? match.isEnabled : true;
                        return (
                          <td key={role} style={{ padding: '16px 24px', textAlign: 'center' }}>
                            <input 
                              type="checkbox"
                              checked={isChecked}
                              onChange={(e) => handleTogglePermission(role, mod.key, e.target.checked)}
                              style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                            />
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
          )}
        </div>
      );
    }

    return null;
  };

  // Add global fetch interceptor to catch any 401 Unauthorized errors in background polling
  useEffect(() => {
    const originalFetch = window.fetch;
    window.fetch = async (...args) => {
      const response = await originalFetch(...args);
      if (response.status === 401) {
        setUser(null);
      }
      return response;
    };
    return () => {
      window.fetch = originalFetch;
    };
  }, []);

  if (isCheckingAuth) {
    return (
      <div style={{ height: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center', backgroundColor: 'var(--bg-color)', flexDirection: 'column', gap: '16px' }}>
        <div style={{ width: '48px', height: '48px', borderRadius: '50%', border: '4px solid var(--border-color)', borderTopColor: 'var(--primary)', animation: 'spin 1s linear infinite' }}></div>
        <p style={{ color: 'var(--text-secondary)', fontWeight: 500 }}>Verifying secure session...</p>
      </div>
    );
  }

  // Render Login page if not authenticated
  if (!user) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center', backgroundColor: 'var(--bg-color)', padding: '20px' }}>
        <div className="premium-card" style={{ width: '100%', maxWidth: '420px', padding: '40px 32px', boxShadow: 'var(--shadow-lg)', border: '1px solid var(--border-color)' }}>
          <div style={{ textAlign: 'center', marginBottom: '32px' }}>
            <img src="/logo.png" alt="Sankalp Retreat" style={{ maxHeight: '60px', marginBottom: '16px' }} />
            <h2 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 8px 0', color: 'var(--text-primary)' }}>Welcome Back</h2>
            <p style={{ color: 'var(--text-secondary)', margin: 0, fontSize: '14px' }}>Sign in to manage WhatsApp Hotel Automation</p>
          </div>

          {loginError && (
            <div style={{ backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', padding: '12px 16px', borderRadius: '6px', fontSize: '14px', marginBottom: '24px', display: 'flex', alignItems: 'center', gap: '8px', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
              <ShieldAlert size={18} />
              <span>{loginError}</span>
            </div>
          )}

          <form onSubmit={handleLogin}>
            <div className="form-group" style={{ marginBottom: '20px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, fontSize: '14px' }}>Username</label>
              <input 
                type="text" 
                value={loginUsername} 
                onChange={e => setLoginUsername(e.target.value)} 
                required 
                placeholder="Enter username"
                style={{ width: '100%', padding: '10px 14px', borderRadius: '6px', border: '1px solid var(--border-color)', outline: 'none', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)' }} 
              />
            </div>

            <div className="form-group" style={{ marginBottom: '28px' }}>
              <label style={{ display: 'block', marginBottom: '8px', fontWeight: 500, fontSize: '14px' }}>Password</label>
              <input 
                type="password" 
                value={loginPassword} 
                onChange={e => setLoginPassword(e.target.value)} 
                required 
                placeholder="Enter password"
                style={{ width: '100%', padding: '10px 14px', borderRadius: '6px', border: '1px solid var(--border-color)', outline: 'none', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)' }} 
              />
            </div>

            <button type="submit" className="btn btn-primary" style={{ width: '100%', padding: '12px', borderRadius: '6px', fontWeight: 600, display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '8px' }}>
              <Lock size={18} /> Sign In
            </button>
          </form>
        </div>
      </div>
    );
  }

  if (prefContactId) {
    return <PreferenceCenter contactId={parseInt(prefContactId)} />;
  }

  return (
    <div className="app-container" style={{ display: 'flex' }}>
      <aside className={`sidebar ${sidebarOpen ? 'open' : 'closed'}`}>
        <div className="sidebar-header">
          <img
            src="/logo.png"
            alt="Sankalp Retreat"
            className="sidebar-logo"
            style={{ maxHeight: '48px', objectFit: 'contain' }}
          />
        </div>
        <nav style={{ display: 'flex', flexDirection: 'column', gap: '8px', padding: '12px 0' }}>
          {hasAccess('dashboard') && (
            <div className={`nav-item ${activeTab === 'dashboard' ? 'active' : ''}`} onClick={() => setActiveTab('dashboard')}>
              <LayoutDashboard size={20} /> Dashboard
            </div>
          )}
          {hasAccess('contacts') && (
            <div className={`nav-item ${activeTab === 'contacts' ? 'active' : ''}`} onClick={() => setActiveTab('contacts')}>
              <Users size={20} /> Contacts
            </div>
          )}
          {hasAccess('templates') && (
            <div className={`nav-item ${activeTab === 'templates' ? 'active' : ''}`} onClick={() => setActiveTab('templates')}>
              <FileText size={20} /> Templates
            </div>
          )}
          {hasAccess('broadcasts') && (
            <div className={`nav-item ${activeTab === 'broadcasts' ? 'active' : ''}`} onClick={() => setActiveTab('broadcasts')}>
              <Megaphone size={20} /> Broadcasts
            </div>
          )}
          
          {/* Developer, Manager & Owner only tab */}
          {hasAccess('rules') && (user.role === 'Owner' || user.role === 'Developer' || user.role === 'Manager') && (
            <div className={`nav-item ${activeTab === 'rules' ? 'active' : ''}`} onClick={() => setActiveTab('rules')}>
              <Workflow size={20} /> Automation
            </div>
          )}

          {hasAccess('analytics') && (
            <div className={`nav-item ${activeTab === 'analytics' ? 'active' : ''}`} onClick={() => setActiveTab('analytics')}>
              <BarChart2 size={20} /> Analytics
            </div>
          )}
          {hasAccess('optouts') && (
            <div className={`nav-item ${activeTab === 'optouts' ? 'active' : ''}`} onClick={() => setActiveTab('optouts')}>
              <UserX size={20} /> Opt-Outs
            </div>
          )}

          {/* Owner & Developer user management tab */}
          {(user.role === 'Owner' || user.role === 'Developer') && (
            <div className={`nav-item ${activeTab === 'users' ? 'active' : ''}`} onClick={() => setActiveTab('users')}>
              <UserCheck size={20} /> User Accounts
            </div>
          )}

          <div className="sidebar-divider" style={{ margin: '8px 16px', borderBottom: '1px solid var(--border)' }}></div>
          {hasAccess('manual-checkin') && (
            <div className={`nav-item ${activeTab === 'manual-checkin' ? 'active' : ''}`} onClick={() => setActiveTab('manual-checkin')}>
              <Hotel size={20} /> Manual Check-In
            </div>
          )}
          {hasAccess('manual-checkout') && (
            <div className={`nav-item ${activeTab === 'manual-checkout' ? 'active' : ''}`} onClick={() => setActiveTab('manual-checkout')}>
              <CheckCircle2 size={20} /> Manual Check-Out
            </div>
          )}
        </nav>
      </aside>

      <main className="main-content" style={{ flex: 1, overflowX: 'hidden', transition: 'margin-left 0.3s ease' }}>
        <header className="topbar" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border-color)', padding: '0 24px', minHeight: '72px' }}>
          <div className="topbar-title" style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <button className="btn btn-secondary" style={{ padding: '6px' }} onClick={() => setSidebarOpen(!sidebarOpen)}>
              <Menu size={20} />
            </button>
            <h2 style={{ fontSize: '18px', fontWeight: 600, margin: 0 }}>
              {activeTab === 'rules' ? 'Automation' : activeTab === 'users' ? 'User Accounts' : activeTab.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ')}
            </h2>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <div style={{ textAlign: 'right', display: 'flex', flexDirection: 'column' }}>
              <span style={{ fontSize: '14px', fontWeight: 600 }}>{user.username}</span>
              <span style={{ fontSize: '11px', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.5px' }}>{user.role}</span>
            </div>
            {user.isImpersonating ? (
              <button className="btn btn-primary" style={{ padding: '8px 14px', backgroundColor: 'var(--danger)', borderColor: 'var(--danger)' }} onClick={handleStopImpersonating}>
                Exit Impersonation
              </button>
            ) : (
              <button className="btn btn-secondary" style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '8px 14px', color: 'var(--danger)', borderColor: 'rgba(239,68,68,0.2)' }} onClick={handleLogout}>
                <LogOut size={16} /> Sign Out
              </button>
            )}
          </div>
        </header>

        {user.isImpersonating && (
          <div style={{ backgroundColor: 'rgba(239,68,68,0.1)', borderBottom: '1px solid rgba(239,68,68,0.2)', padding: '12px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', color: 'var(--danger)', fontWeight: 600, fontSize: '14px' }}>
            <span>⚠️ Currently impersonating user: "{user.username}" ({user.role}). You are viewing the app exactly as they see it.</span>
            <button className="btn btn-primary" style={{ padding: '4px 12px', fontSize: '12px', backgroundColor: 'var(--danger)', borderColor: 'var(--danger)' }} onClick={handleStopImpersonating}>
              Exit Impersonation
            </button>
          </div>
        )}

        <div className="page-content">
          {renderContent()}
        </div>
      </main>

      {/* Create User Modal (Owner Only) */}
      {showCreateUserModal && createPortal((
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
          <div className="premium-analytics-modal" style={{ width: '100%', maxWidth: '440px', padding: '28px', borderRadius: '12px', backgroundColor: 'var(--surface-color)', border: '1px solid var(--border-color)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>Add New User</h3>
              <button onClick={() => setShowCreateUserModal(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><XCircle size={20} /></button>
            </div>

            {createUserError && (
              <div style={{ backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', padding: '10px 14px', borderRadius: '6px', fontSize: '13px', marginBottom: '16px', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
                {createUserError}
              </div>
            )}

            <form onSubmit={handleCreateUser}>
              <div className="form-group" style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '14px' }}>Username</label>
                <input 
                  type="text" 
                  value={newUserUsername} 
                  onChange={e => setNewUserUsername(e.target.value)} 
                  required 
                  placeholder="e.g. receptionist"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-color)', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)' }} 
                />
              </div>

              <div className="form-group" style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '14px' }}>Password</label>
                <input 
                  type="password" 
                  value={newUserPassword} 
                  onChange={e => setNewUserPassword(e.target.value)} 
                  required 
                  placeholder="At least 6 characters"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-color)', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)' }} 
                />
              </div>

              <div className="form-group" style={{ marginBottom: '24px' }}>
                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '14px' }}>Access Role</label>
                <select 
                  value={newUserRole} 
                  onChange={e => setNewUserRole(e.target.value)} 
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-color)', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)' }}
                >
                  <option value="Employer">Employer (Access given by Owner)</option>
                  <option value="Manager">Manager (Access given by Owner)</option>
                  <option value="Developer">Developer (100% System Access)</option>
                  <option value="Owner">Owner (100% Access & User management)</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="btn btn-secondary" onClick={() => setShowCreateUserModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Create Account</button>
              </div>
            </form>
          </div>
        </div>
      ), document.body)}

      {/* Reset Password Modal (Owner Only) */}
      {showResetPasswordModal && createPortal((
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: 'rgba(15, 23, 42, 0.4)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(4px)' }}>
          <div className="premium-analytics-modal" style={{ width: '100%', maxWidth: '440px', padding: '28px', borderRadius: '12px', backgroundColor: 'var(--surface-color)', border: '1px solid var(--border-color)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>Reset Password</h3>
              <button onClick={() => setShowResetPasswordModal(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><XCircle size={20} /></button>
            </div>

            <p style={{ fontSize: '14px', color: 'var(--text-secondary)', marginBottom: '16px' }}>
              Resetting password for user: <strong>{resetPasswordUsername}</strong>
            </p>

            {resetPasswordError && (
              <div style={{ backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', padding: '10px 14px', borderRadius: '6px', fontSize: '13px', marginBottom: '16px', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
                {resetPasswordError}
              </div>
            )}

            <form onSubmit={handleResetPassword}>
              <div className="form-group" style={{ marginBottom: '24px' }}>
                <label style={{ display: 'block', marginBottom: '6px', fontWeight: 500, fontSize: '14px' }}>New Password</label>
                <input 
                  type="password" 
                  value={newPasswordForUser} 
                  onChange={e => setNewPasswordForUser(e.target.value)} 
                  required 
                  placeholder="At least 6 characters"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-color)', backgroundColor: 'var(--surface-color)', color: 'var(--text-primary)' }} 
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="btn btn-secondary" onClick={() => setShowResetPasswordModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary">Reset Password</button>
              </div>
            </form>
          </div>
        </div>
      ), document.body)}
    </div>
  );
}
