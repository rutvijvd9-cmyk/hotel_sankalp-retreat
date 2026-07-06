import React, { useState, useEffect } from 'react';
import { LayoutDashboard, MessagesSquare, Workflow, ScrollText, Send, Hotel, CheckCircle2, AlertCircle, Clock } from 'lucide-react';

const API_BASE = 'http://localhost:5000/api';

export default function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [stats, setStats] = useState({ templates: 0, rules: 0, outbox: 0 });
  const [outbox, setOutbox] = useState([]);
  const [isFiring, setIsFiring] = useState(false);

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
    } catch (e) {
      console.error(e);
    }
  };

  const handleTestEvent = async () => {
    setIsFiring(true);
    try {
      await fetch(`${API_BASE}/test-event`, { method: 'POST' });
      await new Promise(r => setTimeout(r, 1000));
      await fetchData();
    } catch (e) {
      alert('Ensure the C# Backend API is running!');
    }
    setIsFiring(false);
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
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>1</div>
            </div>
            <div className="card" style={{ flex: 1 }}>
              <div style={{ color: 'var(--text-secondary)', marginBottom: '8px' }}>Templates</div>
              <div style={{ fontSize: '32px', fontWeight: 'bold' }}>1</div>
            </div>
          </div>

          <div className="card" style={{ marginBottom: '32px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <div>
              <h3 style={{ marginBottom: '8px' }}>System Simulator</h3>
              <p style={{ color: 'var(--text-secondary)' }}>Fire a simulated PMS CheckOut event to test the entire automation pipeline.</p>
            </div>
            <button className="btn btn-primary" onClick={handleTestEvent} disabled={isFiring}>
              <Send size={18} /> {isFiring ? 'Processing...' : 'Trigger CheckOut Event'}
            </button>
          </div>

          <div className="card">
            <h3 className="card-title">Recent Outbox Activity</h3>
            {outbox.length === 0 ? (
              <div className="empty-state">No outbox messages found. Click the button above to simulate an event.</div>
            ) : (
              <div className="data-table-container">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Phone Number</th>
                      <th>Status</th>
                      <th>Retries</th>
                      <th>Created At</th>
                    </tr>
                  </thead>
                  <tbody>
                    {outbox.map(msg => (
                      <tr key={msg.id}>
                        <td>#{msg.id}</td>
                        <td style={{ fontFamily: 'monospace' }}>{msg.toPhoneNumber}</td>
                        <td>
                          <span className={`badge badge-${msg.status.toLowerCase()}`}>
                            {msg.status === 'Sent' && <CheckCircle2 size={12} style={{marginRight:'4px', verticalAlign:'middle'}}/>}
                            {msg.status === 'Failed' && <AlertCircle size={12} style={{marginRight:'4px', verticalAlign:'middle'}}/>}
                            {msg.status === 'Pending' && <Clock size={12} style={{marginRight:'4px', verticalAlign:'middle'}}/>}
                            {msg.status}
                          </span>
                        </td>
                        <td>{msg.retryCount}</td>
                        <td>{new Date(msg.createdAt).toLocaleString()}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      );
    }

    return (
      <div className="card">
        <div className="empty-state">
          <h3>{activeTab.charAt(0).toUpperCase() + activeTab.slice(1)} Module</h3>
          <p>This module is under construction.</p>
        </div>
      </div>
    );
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
