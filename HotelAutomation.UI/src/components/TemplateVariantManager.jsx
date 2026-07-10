import React, { useState, useEffect } from 'react';
import { Plus, Trash2, BarChart2, FlaskConical, ChevronDown, ChevronUp } from 'lucide-react';

const API_BASE = '/api';

export default function TemplateVariantManager({ templateId, templateName }) {
  const [variants, setVariants] = useState([]);
  const [analytics, setAnalytics] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);
  const [newVariant, setNewVariant] = useState({ VariantName: '', Body: '', Weight: 50 });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const loadData = async () => {
    try {
      const [variantsRes, analyticsRes] = await Promise.all([
        fetch(`${API_BASE}/templates/${templateId}/variants`),
        fetch(`${API_BASE}/templates/${templateId}/variants/analytics`)
      ]);
      if (variantsRes.ok) setVariants(await variantsRes.json());
      if (analyticsRes.ok) setAnalytics(await analyticsRes.json());
    } catch (e) {
      console.error('Failed to load variant data', e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (expanded) loadData();
  }, [expanded, templateId]);

  const handleAdd = async () => {
    if (!newVariant.VariantName.trim()) return setError('Variant name is required.');
    if (!newVariant.Body.trim()) return setError('Message body is required.');
    if (newVariant.Weight < 1 || newVariant.Weight > 100) return setError('Weight must be between 1 and 100.');
    setError('');
    setSaving(true);
    try {
      const res = await fetch(`${API_BASE}/templates/${templateId}/variants`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newVariant)
      });
      if (res.ok) {
        setNewVariant({ VariantName: '', Body: '', Weight: 50 });
        setShowAddForm(false);
        await loadData();
      } else {
        const data = await res.json();
        setError(data.message || 'Failed to add variant.');
      }
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (variantId) => {
    if (!window.confirm('Delete this variant? Messages already sent will retain their variant record.')) return;
    await fetch(`${API_BASE}/templates/${templateId}/variants/${variantId}`, { method: 'DELETE' });
    await loadData();
  };

  // Find analytics for a given variantId
  const getAnalytics = (variantId) =>
    analytics.find(a => a.VariantId === variantId || a.variantId === variantId) || { TotalSent: 0, Delivered: 0, Failed: 0 };

  // Total weight for percentage display
  const totalWeight = variants.reduce((s, v) => s + (v.Weight || v.weight || 0), 0);

  return (
    <div style={{ marginTop: '8px', border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
      {/* Header */}
      <button
        id={`variant-toggle-${templateId}`}
        onClick={() => setExpanded(e => !e)}
        style={{
          width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '10px 14px', background: 'var(--bg-secondary)', border: 'none', cursor: 'pointer',
          color: 'var(--text-primary)', fontSize: '0.85em', fontWeight: 500, transition: 'background 0.15s'
        }}
      >
        <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <FlaskConical size={15} color="var(--primary)" />
          A/B Variants {variants.length > 0 ? `(${variants.length} active)` : ''}
        </span>
        {expanded ? <ChevronUp size={15} /> : <ChevronDown size={15} />}
      </button>

      {expanded && (
        <div style={{ padding: '14px', background: 'var(--bg-primary)' }}>
          {loading ? (
            <div style={{ color: 'var(--text-secondary)', fontSize: '0.85em', padding: '8px 0' }}>Loading…</div>
          ) : (
            <>
              {/* Variant list */}
              {variants.length === 0 && !showAddForm && (
                <div style={{ color: 'var(--text-secondary)', fontSize: '0.85em', marginBottom: '12px' }}>
                  No variants yet. Add one to start A/B testing this template.
                </div>
              )}

              {variants.map(v => {
                const stats = getAnalytics(v.Id || v.id);
                const weight = v.Weight || v.weight || 0;
                const pct = totalWeight > 0 ? Math.round((weight / totalWeight) * 100) : 0;
                const totalSent = stats.TotalSent || stats.totalSent || 0;
                const delivered = stats.Delivered || stats.delivered || 0;
                const failed = stats.Failed || stats.failed || 0;
                const deliveryRate = totalSent > 0 ? Math.round((delivered / totalSent) * 100) : null;

                return (
                  <div key={v.Id || v.id} style={{
                    padding: '12px', marginBottom: '10px', borderRadius: '6px',
                    border: '1px solid var(--border-color)', background: 'var(--bg-secondary)',
                    transition: 'border-color 0.15s'
                  }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '6px' }}>
                      <div>
                        <span style={{ fontWeight: 600, fontSize: '0.9em' }}>{v.VariantName || v.variantName}</span>
                        <span style={{
                          marginLeft: '8px', padding: '2px 8px', borderRadius: '12px',
                          background: 'var(--primary)', color: '#fff', fontSize: '0.75em', fontWeight: 600
                        }}>{pct}% traffic</span>
                      </div>
                      <button
                        onClick={() => handleDelete(v.Id || v.id)}
                        style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#ef4444', padding: '2px' }}
                        title="Delete variant"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>

                    {/* Body preview */}
                    <div style={{
                      fontSize: '0.82em', color: 'var(--text-secondary)', marginBottom: '8px',
                      whiteSpace: 'pre-wrap', wordBreak: 'break-word', maxHeight: '60px', overflow: 'hidden',
                      borderLeft: '3px solid var(--primary)', paddingLeft: '8px'
                    }}>
                      {(v.Body || v.body || '').slice(0, 150)}{(v.Body || v.body || '').length > 150 ? '…' : ''}
                    </div>

                    {/* Analytics row */}
                    <div style={{ display: 'flex', gap: '16px', fontSize: '0.8em', color: 'var(--text-secondary)' }}>
                      <span><BarChart2 size={12} style={{ verticalAlign: 'middle', marginRight: '3px' }} />{totalSent} sent</span>
                      <span style={{ color: '#10b981' }}>✓ {delivered} delivered</span>
                      {failed > 0 && <span style={{ color: '#ef4444' }}>✗ {failed} failed</span>}
                      {deliveryRate !== null && (
                        <span style={{ marginLeft: 'auto', fontWeight: 600, color: deliveryRate >= 90 ? '#10b981' : deliveryRate >= 70 ? '#f59e0b' : '#ef4444' }}>
                          {deliveryRate}% rate
                        </span>
                      )}
                    </div>

                    {/* Weight bar */}
                    <div style={{ marginTop: '8px', height: '4px', background: 'var(--border-color)', borderRadius: '2px', overflow: 'hidden' }}>
                      <div style={{ width: `${pct}%`, height: '100%', background: 'var(--primary)', borderRadius: '2px', transition: 'width 0.3s' }} />
                    </div>
                  </div>
                );
              })}

              {/* Add form */}
              {showAddForm && (
                <div style={{ padding: '12px', borderRadius: '6px', border: '1px dashed var(--primary)', marginBottom: '10px' }}>
                  {error && <div style={{ color: '#ef4444', fontSize: '0.82em', marginBottom: '8px' }}>{error}</div>}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                    <input
                      id={`variant-name-${templateId}`}
                      className="form-input"
                      placeholder="Variant name (e.g. Version A)"
                      value={newVariant.VariantName}
                      onChange={e => setNewVariant(p => ({ ...p, VariantName: e.target.value }))}
                    />
                    <textarea
                      id={`variant-body-${templateId}`}
                      className="form-input"
                      placeholder="Message body with {{variables}}"
                      value={newVariant.Body}
                      onChange={e => setNewVariant(p => ({ ...p, Body: e.target.value }))}
                      rows={3}
                      style={{ resize: 'vertical', fontFamily: 'monospace', fontSize: '0.85em' }}
                    />
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <label style={{ fontSize: '0.85em', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>Weight:</label>
                      <input
                        id={`variant-weight-${templateId}`}
                        type="number"
                        className="form-input"
                        min={1} max={100}
                        value={newVariant.Weight}
                        onChange={e => setNewVariant(p => ({ ...p, Weight: parseInt(e.target.value) || 1 }))}
                        style={{ width: '80px' }}
                      />
                      <span style={{ fontSize: '0.8em', color: 'var(--text-secondary)' }}>
                        (relative traffic share; higher = more sends)
                      </span>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <button id={`save-variant-${templateId}`} className="btn btn-primary" onClick={handleAdd} disabled={saving} style={{ fontSize: '0.85em', padding: '6px 14px' }}>
                        {saving ? 'Saving…' : 'Save Variant'}
                      </button>
                      <button className="btn btn-secondary" onClick={() => { setShowAddForm(false); setError(''); }} style={{ fontSize: '0.85em', padding: '6px 14px' }}>
                        Cancel
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {!showAddForm && (
                <button
                  id={`add-variant-${templateId}`}
                  className="btn btn-secondary"
                  onClick={() => setShowAddForm(true)}
                  style={{ fontSize: '0.82em', padding: '6px 12px', display: 'flex', alignItems: 'center', gap: '4px' }}
                >
                  <Plus size={13} /> Add Variant
                </button>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}
