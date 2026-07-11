import React, { useState, useEffect } from 'react';
import { Send, Search, CheckCircle2, User, Phone, DoorClosed, DollarSign, Calendar, Lock, Hash, Link, FileText, Loader2, Hotel } from 'lucide-react';

export default function ManualCheckOut() {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [isSearching, setIsSearching] = useState(false);
  
  const [formData, setFormData] = useState({
    GuestName: '',
    PhoneNumber: '',
    RoomNumber: '',
    TotalBill: '',
    CheckInDate: '',
    CheckOutDate: '',
    WifiPassword: '',
    BookingReference: '',
    GuestFeedbackLink: '',
    HotelName: '',
    ExtraNote: ''
  });
  
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const delayDebounceFn = setTimeout(async () => {
      if (searchQuery.length > 2) {
        setIsSearching(true);
        try {
          const res = await fetch(`/api/contacts/search?query=${encodeURIComponent(searchQuery)}`);
          if (res.ok) {
            const data = await res.json();
            setSearchResults(data);
          }
        } catch (err) {
          console.error("Search failed:", err);
        } finally {
          setIsSearching(false);
        }
      } else {
        setSearchResults([]);
      }
    }, 500);

    return () => clearTimeout(delayDebounceFn);
  }, [searchQuery]);

  const handleSelectContact = (contact) => {
    setFormData(prev => ({
      ...prev,
      GuestName: `${contact.firstName || ''} ${contact.lastName || ''}`.trim(),
      PhoneNumber: contact.phoneNumber || ''
    }));
    setSearchQuery('');
    setSearchResults([]);
  };

  const handleChange = (e) => {
    setFormData(prev => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!formData.GuestName || !formData.PhoneNumber) {
      setStatus('error: Name and Phone Number are required.');
      return;
    }
    setLoading(true);
    setStatus('');
    
    try {
      const payload = { ...formData };
      if (payload.TotalBill) payload.TotalBill = parseFloat(payload.TotalBill);

      const res = await fetch('/api/manual/checkout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      
      if (res.ok) {
        setStatus('success');
        setFormData({
          GuestName: '', PhoneNumber: '', RoomNumber: '', TotalBill: '', 
          CheckInDate: '', CheckOutDate: '', WifiPassword: '', 
          BookingReference: '', GuestFeedbackLink: '', HotelName: '', ExtraNote: ''
        });
        setTimeout(() => setStatus(''), 3000);
      } else {
        setStatus('error: Failed to trigger manual check-out.');
      }
    } catch (err) {
      setStatus('error: Network error.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="premium-table-card" style={{ maxWidth: '800px', margin: '40px auto', padding: '32px', animation: 'fadeIn 0.3s ease' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '12px' }}>
        <div style={{ width: '48px', height: '48px', borderRadius: '12px', background: 'linear-gradient(135deg, rgba(59,130,246,0.1) 0%, rgba(37,99,235,0.05) 100%)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <CheckCircle2 size={24} color="var(--primary)" />
        </div>
        <h3 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 'bold', color: 'var(--text-primary)' }}>
          Manual Check-Out
        </h3>
      </div>
      <p style={{ color: 'var(--text-secondary)', marginBottom: '24px', fontSize: '0.95rem' }}>
        Use this form to manually trigger a Check-Out event. You can search for existing contacts to autofill details.
      </p>

      {/* Search Bar */}
      <div style={{ position: 'relative', marginBottom: '32px' }}>
        <div style={{ display: 'flex', alignItems: 'center', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '12px 16px', background: '#fff' }}>
          <Search size={18} color="var(--text-secondary)" style={{ marginRight: '12px' }} />
          <input 
            type="text" 
            placeholder="Search contacts by name or phone..." 
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            style={{ border: 'none', background: 'transparent', outline: 'none', width: '100%', color: 'var(--text-primary)', fontSize: '0.95rem' }}
          />
          {isSearching && <Loader2 size={16} className="spinner" style={{ color: 'var(--text-secondary)' }} />}
        </div>
        
        {searchResults.length > 0 && (
          <div style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: '#fff', border: '1px solid var(--border-color)', borderRadius: '8px', zIndex: 10, marginTop: '8px', boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.1)', maxHeight: '250px', overflowY: 'auto' }}>
            {searchResults.map((contact, idx) => (
              <div 
                key={idx} 
                onClick={() => handleSelectContact(contact)}
                style={{ padding: '16px', borderBottom: idx < searchResults.length - 1 ? '1px solid var(--border-color)' : 'none', cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
                className="hover-bg"
              >
                <div>
                  <div style={{ fontWeight: 600, color: 'var(--text-primary)', marginBottom: '4px' }}>{contact.firstName} {contact.lastName}</div>
                  <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)' }}>{contact.phoneNumber}</div>
                </div>
                <div style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--primary)' }}>Select</div>
              </div>
            ))}
          </div>
        )}
      </div>

      {status.startsWith('error') && (
        <div style={{ padding: '16px', background: '#fef2f2', border: '1px solid #fee2e2', color: '#ef4444', borderRadius: '8px', marginBottom: '24px', fontSize: '0.95rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
          {status.replace('error: ', '')}
        </div>
      )}
      
      {status === 'success' && (
        <div style={{ padding: '16px', background: '#f0fdf4', border: '1px solid #dcfce7', color: '#10b981', borderRadius: '8px', marginBottom: '24px', fontSize: '0.95rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <CheckCircle2 size={20} /> Check-Out event successfully triggered!
        </div>
      )}

      <form onSubmit={handleSubmit} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px' }}>
        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><User size={16} /> Guest Name *</label>
          <input type="text" name="GuestName" value={formData.GuestName} onChange={handleChange} className="premium-chat-input" placeholder="John Doe" required style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>
        
        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Phone size={16} /> Phone Number *</label>
          <input type="text" name="PhoneNumber" value={formData.PhoneNumber} onChange={handleChange} className="premium-chat-input" placeholder="+1234567890" required style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><DoorClosed size={16} /> Room Number</label>
          <input type="text" name="RoomNumber" value={formData.RoomNumber} onChange={handleChange} className="premium-chat-input" placeholder="101" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><DollarSign size={16} /> Total Bill</label>
          <input type="number" step="0.01" name="TotalBill" value={formData.TotalBill} onChange={handleChange} className="premium-chat-input" placeholder="150.00" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Calendar size={16} /> Check-In Date</label>
          <input type="date" name="CheckInDate" value={formData.CheckInDate} onChange={handleChange} className="premium-chat-input" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Calendar size={16} /> Check-Out Date</label>
          <input type="date" name="CheckOutDate" value={formData.CheckOutDate} onChange={handleChange} className="premium-chat-input" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Lock size={16} /> Wifi Password</label>
          <input type="text" name="WifiPassword" value={formData.WifiPassword} onChange={handleChange} className="premium-chat-input" placeholder="Guest@2026" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Hash size={16} /> Booking Reference</label>
          <input type="text" name="BookingReference" value={formData.BookingReference} onChange={handleChange} className="premium-chat-input" placeholder="BK-12345" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>
        
        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Hotel size={16} /> Hotel Name</label>
          <input type="text" name="HotelName" value={formData.HotelName} onChange={handleChange} className="premium-chat-input" placeholder="Sankalp Retreat" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group">
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><Link size={16} /> Feedback Link</label>
          <input type="text" name="GuestFeedbackLink" value={formData.GuestFeedbackLink} onChange={handleChange} className="premium-chat-input" placeholder="https://g.page/review" style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none' }} />
        </div>

        <div className="form-group" style={{ gridColumn: '1 / -1' }}>
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}><FileText size={16} /> Extra Note</label>
          <textarea name="ExtraNote" value={formData.ExtraNote} onChange={handleChange} className="premium-chat-input" placeholder="Any special requests or notes" rows={3} style={{ width: '100%', padding: '12px 16px', borderRadius: '8px', border: '1px solid var(--border-color)', outline: 'none', resize: 'vertical' }}></textarea>
        </div>

        <div style={{ gridColumn: '1 / -1', marginTop: '24px' }}>
          <button type="submit" className="btn btn-primary" disabled={loading} style={{ width: '100%', padding: '14px', fontSize: '1rem', fontWeight: 600, display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '8px', borderRadius: '12px' }}>
            <Send size={20} /> {loading ? 'Triggering Check-Out...' : 'Trigger Check-Out Event'}
          </button>
        </div>
      </form>
      <style>{`
        .hover-bg:hover { background-color: #f1f5f9 !important; }
      `}</style>
    </div>
  );
}
