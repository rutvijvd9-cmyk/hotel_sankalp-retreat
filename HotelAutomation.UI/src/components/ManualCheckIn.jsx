import React, { useState } from 'react';
import { Send, Hotel, CheckCircle2, User, Phone, DoorClosed, DollarSign, Calendar, Lock, Hash, Link, FileText } from 'lucide-react';

export default function ManualCheckIn() {
  const [formData, setFormData] = useState({
    GuestName: '',
    PhoneNumber: '',
    RoomNumber: '',
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

      const res = await fetch('/api/manual/checkin', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      
      if (res.ok) {
        setStatus('success');
        setFormData({
          GuestName: '', PhoneNumber: '', RoomNumber: '', 
          CheckInDate: '', CheckOutDate: '', WifiPassword: '', 
          BookingReference: '', GuestFeedbackLink: '', HotelName: '', ExtraNote: ''
        });
        setTimeout(() => setStatus(''), 3000);
      } else {
        setStatus('error: Failed to trigger manual check-in.');
      }
    } catch (err) {
      setStatus('error: Network error.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="card" style={{ maxWidth: '800px', margin: '0 auto', animation: 'fadeIn 0.3s ease' }}>
      <h3 className="card-title" style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '24px' }}>
        <CheckCircle2 color="var(--primary)" /> Manual Check-In
      </h3>
      <p style={{ color: 'var(--text-secondary)', marginBottom: '24px', fontSize: '0.9rem' }}>
        Use this form to manually trigger a Check-In event when the PMS automated integration is down or bypassed.
      </p>

      {status.startsWith('error') && (
        <div style={{ padding: '12px', background: '#fee2e2', color: '#ef4444', borderRadius: '4px', marginBottom: '16px', fontSize: '0.9rem' }}>
          {status.replace('error: ', '')}
        </div>
      )}
      
      {status === 'success' && (
        <div style={{ padding: '12px', background: '#dcfce7', color: '#10b981', borderRadius: '4px', marginBottom: '16px', fontSize: '0.9rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <CheckCircle2 size={16} /> Check-In event successfully triggered!
        </div>
      )}

      <form onSubmit={handleSubmit} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px' }}>
        <div className="form-group">
          <label><User size={14} style={{ marginRight: '4px' }}/> Guest Name *</label>
          <input type="text" name="GuestName" value={formData.GuestName} onChange={handleChange} className="form-control" placeholder="John Doe" required />
        </div>
        
        <div className="form-group">
          <label><Phone size={14} style={{ marginRight: '4px' }}/> Phone Number *</label>
          <input type="text" name="PhoneNumber" value={formData.PhoneNumber} onChange={handleChange} className="form-control" placeholder="+1234567890" required />
        </div>

        <div className="form-group">
          <label><DoorClosed size={14} style={{ marginRight: '4px' }}/> Room Number</label>
          <input type="text" name="RoomNumber" value={formData.RoomNumber} onChange={handleChange} className="form-control" placeholder="101" />
        </div>



        <div className="form-group">
          <label><Calendar size={14} style={{ marginRight: '4px' }}/> Check-In Date</label>
          <input type="date" name="CheckInDate" value={formData.CheckInDate} onChange={handleChange} className="form-control" />
        </div>

        <div className="form-group">
          <label><Calendar size={14} style={{ marginRight: '4px' }}/> Check-Out Date</label>
          <input type="date" name="CheckOutDate" value={formData.CheckOutDate} onChange={handleChange} className="form-control" />
        </div>

        <div className="form-group">
          <label><Lock size={14} style={{ marginRight: '4px' }}/> Wifi Password</label>
          <input type="text" name="WifiPassword" value={formData.WifiPassword} onChange={handleChange} className="form-control" placeholder="Guest@2026" />
        </div>

        <div className="form-group">
          <label><Hash size={14} style={{ marginRight: '4px' }}/> Booking Reference</label>
          <input type="text" name="BookingReference" value={formData.BookingReference} onChange={handleChange} className="form-control" placeholder="BK-12345" />
        </div>
        
        <div className="form-group">
          <label><Hotel size={14} style={{ marginRight: '4px' }}/> Hotel Name</label>
          <input type="text" name="HotelName" value={formData.HotelName} onChange={handleChange} className="form-control" placeholder="Sankalp Retreat" />
        </div>

        <div className="form-group">
          <label><Link size={14} style={{ marginRight: '4px' }}/> Feedback Link</label>
          <input type="text" name="GuestFeedbackLink" value={formData.GuestFeedbackLink} onChange={handleChange} className="form-control" placeholder="https://g.page/review" />
        </div>

        <div className="form-group" style={{ gridColumn: '1 / -1' }}>
          <label><FileText size={14} style={{ marginRight: '4px' }}/> Extra Note</label>
          <textarea name="ExtraNote" value={formData.ExtraNote} onChange={handleChange} className="form-control" placeholder="Any special requests or notes" rows={2}></textarea>
        </div>

        <div style={{ gridColumn: '1 / -1', marginTop: '16px' }}>
          <button type="submit" className="btn btn-primary" disabled={loading} style={{ width: '100%', padding: '12px', fontSize: '1rem', display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '8px' }}>
            <Send size={18} /> {loading ? 'Triggering Check-In...' : 'Trigger Check-In Event'}
          </button>
        </div>
      </form>
    </div>
  );
}
