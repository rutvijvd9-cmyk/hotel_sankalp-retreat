import React, { useState, useEffect } from 'react';

const API_BASE = '/api';

export default function PreferenceCenter({ contactId }) {
  const [preferences, setPreferences] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    fetch(`${API_BASE}/preferences/${contactId}`)
      .then(res => res.json())
      .then(data => {
        setPreferences(data);
        setLoading(false);
      })
      .catch(err => {
        console.error(err);
        setLoading(false);
      });
  }, [contactId]);

  const handleToggle = (categoryId) => {
    setPreferences(prev => prev.map(p => 
      p.CategoryId === categoryId || p.categoryId === categoryId 
        ? { ...p, IsSubscribed: p.IsSubscribed === 1 ? 0 : 1, isSubscribed: p.IsSubscribed === 1 ? 0 : 1 } 
        : p
    ));
  };

  const handleSave = async () => {
    setSaving(true);
    setMessage('');
    try {
      const updates = preferences.map(p => ({
        CategoryId: p.CategoryId || p.categoryId,
        IsSubscribed: (p.IsSubscribed ?? p.isSubscribed) === 1
      }));
      
      const res = await fetch(`${API_BASE}/preferences/${contactId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(updates)
      });
      
      if (res.ok) {
        setMessage('Your preferences have been updated successfully.');
      } else {
        setMessage('Error saving preferences. Please try again.');
      }
    } catch (err) {
      setMessage('Error saving preferences. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>Loading preferences...</div>;
  }

  return (
    <div style={{ maxWidth: '600px', margin: '40px auto', fontFamily: 'Inter, sans-serif' }}>
      <div className="card" style={{ padding: '32px' }}>
        <div style={{ textAlign: 'center', marginBottom: '32px' }}>
          <h1 style={{ fontSize: '1.8em', color: 'var(--text-primary)' }}>Communication Preferences</h1>
          <p style={{ color: 'var(--text-secondary)', marginTop: '8px' }}>
            Manage the types of messages you receive from Sankalp Retreat.
          </p>
        </div>

        {message && (
          <div style={{ padding: '16px', marginBottom: '24px', borderRadius: '6px', backgroundColor: message.includes('Error') ? '#fee2e2' : '#dcfce7', color: message.includes('Error') ? '#991b1b' : '#166534', textAlign: 'center' }}>
            {message}
          </div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginBottom: '32px' }}>
          {preferences.filter(p => (p.CategoryName || p.categoryName) !== 'Utility').map(p => {
            const isSubbed = (p.IsSubscribed ?? p.isSubscribed) === 1;
            const catId = p.CategoryId || p.categoryId;
            const catName = p.CategoryName || p.categoryName;
            
            return (
              <div key={catId} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '16px', border: '1px solid var(--border-color)', borderRadius: '8px', backgroundColor: 'var(--bg-secondary)' }}>
                <div>
                  <div style={{ fontWeight: 600, fontSize: '1.1em' }}>{catName}</div>
                  <div style={{ fontSize: '0.85em', color: 'var(--text-secondary)', marginTop: '4px' }}>
                    {catName === 'Marketing' ? 'Promotions, special offers, and newsletters.' : `Receive ${catName.toLowerCase()} updates.`}
                  </div>
                </div>
                <div>
                  <label style={{ display: 'flex', alignItems: 'center', cursor: 'pointer' }}>
                    <div style={{ position: 'relative' }}>
                      <input 
                        type="checkbox" 
                        checked={isSubbed} 
                        onChange={() => handleToggle(catId)}
                        style={{ opacity: 0, width: 0, height: 0 }}
                      />
                      <div style={{ 
                        width: '44px', height: '24px', backgroundColor: isSubbed ? 'var(--primary)' : '#cbd5e1', 
                        borderRadius: '12px', transition: 'background-color 0.2s', position: 'relative'
                      }}>
                        <div style={{
                          position: 'absolute', top: '2px', left: isSubbed ? '22px' : '2px',
                          width: '20px', height: '20px', backgroundColor: 'white', borderRadius: '50%',
                          transition: 'left 0.2s', boxShadow: '0 1px 3px rgba(0,0,0,0.2)'
                        }} />
                      </div>
                    </div>
                  </label>
                </div>
              </div>
            );
          })}
        </div>

        <button 
          className="btn btn-primary" 
          onClick={handleSave} 
          disabled={saving}
          style={{ width: '100%', padding: '12px', fontSize: '1.1em', justifyContent: 'center' }}
        >
          {saving ? 'Saving...' : 'Save Preferences'}
        </button>

        <div style={{ marginTop: '24px', textAlign: 'center', fontSize: '0.8em', color: 'var(--text-secondary)' }}>
          Note: Utility messages (like booking confirmations) cannot be opted out of as they are required for your stay.
        </div>
      </div>
    </div>
  );
}
