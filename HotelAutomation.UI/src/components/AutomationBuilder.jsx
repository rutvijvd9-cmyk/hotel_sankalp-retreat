import React, { useState } from 'react';
import { 
  Zap, Clock, MessageSquare, GitBranch, XCircle, Plus, 
  Trash2, ChevronDown, ChevronUp, Save, CheckCircle2
} from 'lucide-react';

const STEP_TYPES = {
  TRIGGER: {
    label: 'Trigger',
    color: '#3B82F6',
    bg: 'rgba(59,130,246,0.1)',
    border: '#3B82F6',
    icon: Zap,
    category: 'Start',
    desc: 'When the automation starts',
  },
  WAIT: {
    label: 'Wait',
    color: '#F59E0B',
    bg: 'rgba(245,158,11,0.1)',
    border: '#F59E0B',
    icon: Clock,
    category: 'Logic',
    desc: 'Pause before continuing',
  },
  CONDITION: {
    label: 'Condition',
    color: '#8B5CF6',
    bg: 'rgba(139,92,246,0.1)',
    border: '#8B5CF6',
    icon: GitBranch,
    category: 'Logic',
    desc: 'Branch based on contact data',
  },
  WHATSAPP: {
    label: 'Send WhatsApp',
    color: '#10B981',
    bg: 'rgba(16,185,129,0.1)',
    border: '#10B981',
    icon: MessageSquare,
    category: 'Actions',
    desc: 'Send a WhatsApp message',
  },
  STOP: {
    label: 'Stop',
    color: '#EF4444',
    bg: 'rgba(239,68,68,0.1)',
    border: '#EF4444',
    icon: XCircle,
    category: 'Finishers',
    desc: 'End the workflow',
  },
};

// ─── Trigger Options (grouped for UI) ────────────────────────────────────────
// Each entry: { value, label, group }
// 'group' is used in the UI to render <optgroup> separators.
const TRIGGER_OPTIONS = [
  // PMS / Booking Lifecycle
  { value: 'PMS_CHECKIN',             label: 'PMS Check-in',             group: 'PMS / Booking Lifecycle' },
  { value: 'PMS_CHECKOUT',            label: 'PMS Check-out',            group: 'PMS / Booking Lifecycle' },
  { value: 'BOOKING_CREATED',         label: 'Booking Created',          group: 'PMS / Booking Lifecycle' },
  { value: 'BOOKING_MODIFIED',        label: 'Booking Modified',         group: 'PMS / Booking Lifecycle' },
  { value: 'BOOKING_CANCELLED',       label: 'Booking Cancelled',        group: 'PMS / Booking Lifecycle' },
  // Guest Activity
  { value: 'ROOM_SERVICE_REQUEST',    label: 'Room Service Request',     group: 'Guest Activity' },
  { value: 'PAYMENT_RECEIVED',        label: 'Payment Received',         group: 'Guest Activity' },
  { value: 'COMPLAINT_RAISED',        label: 'Complaint / Issue Raised', group: 'Guest Activity' },
  // Special
  { value: 'MANUAL_TRIGGER',          label: 'Manual / One-Time Send',   group: 'Special' },
  { value: 'EVENT_API',               label: 'Event / API Variable',     group: 'Special' },
];

function StepTypePicker({ onSelect, onClose }) {
  const popupRef = React.useRef(null);
  
  React.useEffect(() => {
    if (popupRef.current) {
      popupRef.current.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, []);

  return (
    <>
      <div style={{ position: 'fixed', inset: 0, zIndex: 199 }} onClick={onClose} ></div>
      <div ref={popupRef} style={{
        position: 'absolute', top: 36, left: '50%', transform: 'translateX(-50%)',
        background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e2e8f0)',
        borderRadius: 14, boxShadow: '0 12px 32px rgba(0,0,0,0.18)',
        zIndex: 200, overflow: 'hidden', width: 220, padding: '0.5rem',
      }}>
        {['Actions', 'Logic', 'Finishers'].map(cat => (
          <div key={cat} style={{ marginBottom: '0.5rem' }}>
            <div style={{ fontSize: '0.6rem', fontWeight: 800, color: 'var(--text-sub, #64748b)', padding: '0.25rem 0.75rem', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              {cat}
            </div>
            {Object.entries(STEP_TYPES).filter(([k, v]) => k !== 'TRIGGER' && v.category === cat).map(([key, cfg]) => {
              const Icon = cfg.icon;
              return (
                <button key={key} onClick={() => { onSelect(key); onClose(); }} style={{
                  width: '100%', display: 'flex', alignItems: 'center', gap: '0.75rem',
                  padding: '0.6rem 0.75rem', border: 'none', background: 'none',
                  cursor: 'pointer', textAlign: 'left', color: 'var(--text-heading, #1e293b)',
                  fontSize: '0.82rem', borderRadius: 8, transition: 'background .15s',
                }}
                  onMouseEnter={e => e.currentTarget.style.background = 'var(--hover, #f1f5f9)'}
                  onMouseLeave={e => e.currentTarget.style.background = 'none'}
                >
                  <div style={{ width: 28, height: 28, borderRadius: 7, background: cfg.bg, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                    <Icon size={13} color={cfg.color} />
                  </div>
                  <span style={{ fontWeight: 500 }}>{cfg.label}</span>
                </button>
              );
            })}
          </div>
        ))}
      </div>
    </>
  );
}

function AddBtn({ onAdd }) {
  const [open, setOpen] = useState(false);
  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', position: 'relative', margin: '4px 0' }}>
      <div style={{ width: 2, height: 18, background: 'var(--border, #e2e8f0)' }} ></div>
      <div style={{ position: 'relative' }}>
        <button
          onClick={() => setOpen(o => !o)}
          title="Add step"
          style={{
            width: 26, height: 26, borderRadius: '50%',
            border: '2px dashed var(--border, #e2e8f0)', background: 'var(--bg, #f8fafc)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            cursor: 'pointer', color: 'var(--text-sub, #64748b)', transition: 'all .18s',
          }}
          onMouseEnter={e => { e.currentTarget.style.borderColor = '#6366F1'; e.currentTarget.style.color = '#6366F1'; e.currentTarget.style.background = 'rgba(99,102,241,0.08)' }}
          onMouseLeave={e => { e.currentTarget.style.borderColor = 'var(--border, #e2e8f0)'; e.currentTarget.style.color = 'var(--text-sub, #64748b)'; e.currentTarget.style.background = 'var(--bg, #f8fafc)' }}
        >
          <Plus size={12} />
        </button>
        {open && <StepTypePicker onSelect={onAdd} onClose={() => setOpen(false)} />}
      </div>
      <div style={{ width: 2, height: 18, background: 'var(--border, #e2e8f0)' }} ></div>
    </div>
  );
}

function StepCard({ step, onUpdate, onDelete, canDelete = true, allTriggerOptions = [] }) {
  const [isExpanded, setIsExpanded] = useState(true);
  const cfg = STEP_TYPES[step.stepType] || STEP_TYPES.WHATSAPP;
  const Icon = cfg.icon;

  return (
    <div style={{
      border: `2px solid ${cfg.border}`,
      borderRadius: 14,
      background: 'var(--card-bg, #ffffff)',
      overflow: 'hidden',
      boxShadow: '0 2px 14px rgba(0,0,0,0.07)',
      width: '100%',
      maxWidth: '400px',
      margin: '0 auto'
    }}>
      <div style={{
        background: cfg.bg,
        padding: '0.7rem 0.9rem',
        display: 'flex', alignItems: 'center', gap: '0.65rem', cursor: 'pointer',
        userSelect: 'none',
      }} onClick={() => setIsExpanded(e => !e)}>
        <div style={{ width: 32, height: 32, borderRadius: 8, background: `${cfg.color}22`, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
          <Icon size={15} color={cfg.color} />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: '0.78rem', fontWeight: 700, color: cfg.color }}>{cfg.label}</div>
          <div style={{ fontSize: '0.68rem', color: 'var(--text-sub, #64748b)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {step.stepType === 'TRIGGER' && (
              step.triggerValue === 'EVENT_API'
                ? `API Slug: ${step.eventApiSlug || 'not set'}`
                : (allTriggerOptions.find(o => o.value === step.triggerValue)?.label || 'Manual')
            )}
            {step.stepType === 'WAIT' && `Wait ${step.waitValue || 1} ${step.waitUnit || 'HOURS'}`}
            {step.stepType === 'WHATSAPP' && (step.templateId ? `Template: ${step.templateId}` : 'No template selected')}
            {step.stepType === 'CONDITION' && 'Check contact conditions'}
            {step.stepType === 'STOP' && 'Workflow ends'}
          </div>
        </div>
        <div style={{ display: 'flex', gap: '0.2rem', flexShrink: 0 }}>
          {isExpanded ? <ChevronUp size={13} color="var(--text-sub, #64748b)" /> : <ChevronDown size={13} color="var(--text-sub, #64748b)" />}
          {canDelete && step.stepType !== 'TRIGGER' && (
            <button
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 2, color: '#EF4444', lineHeight: 1, borderRadius: 4, transition: 'background .15s' }}
              onClick={e => { e.stopPropagation(); onDelete(step.id); }}
              title="Delete step"
              onMouseEnter={e => e.currentTarget.style.background = 'rgba(239,68,68,0.1)'}
              onMouseLeave={e => e.currentTarget.style.background = 'none'}
            >
              <Trash2 size={13} />
            </button>
          )}
        </div>
      </div>

      {isExpanded && (
        <div style={{ padding: '0.85rem' }}>
          {step.stepType === 'TRIGGER' && (
            <div style={{ margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              <label style={{ fontSize: '0.75rem', fontWeight: 500 }}>Trigger Event</label>
              <select
                style={{ padding: '6px 8px', borderRadius: '6px', border: '1px solid var(--border, #e2e8f0)', fontSize: '0.82rem', background: 'var(--card-bg, #fff)', color: 'var(--text-heading, #1e293b)' }}
                value={step.triggerValue || 'PMS_CHECKIN'}
                onChange={e => onUpdate(step.id, { triggerValue: e.target.value })}
              >
                {/* PMS static options rendered as grouped optgroups */}
                {(() => {
                  const groups = {};
                  allTriggerOptions.forEach(o => {
                    const g = o.group || 'Other';
                    if (!groups[g]) groups[g] = [];
                    groups[g].push(o);
                  });
                  return Object.entries(groups).map(([groupName, opts]) => (
                    <optgroup key={groupName} label={groupName}>
                      {opts.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </optgroup>
                  ));
                })()}
              </select>

              {step.triggerValue && step.triggerValue.startsWith('DATE_') && (
                <div style={{ marginTop: '8px', padding: '12px', background: '#f8fafc', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                  <label style={{ fontSize: '0.75rem', fontWeight: 600, display: 'block', marginBottom: '8px', color: '#0f172a' }}>Time Offset (Days Before)</label>
                  <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                    <input type="number" 
                      value={step.offsetDays || 0} 
                      onChange={e => onUpdate(step.id, { offsetDays: e.target.value })}
                      style={{ width: '80px', padding: '6px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                    <span style={{ fontSize: '0.75rem', color: '#64748b' }}>days before the date</span>
                  </div>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '12px', fontSize: '0.75rem', color: '#475569' }}>
                    <input type="checkbox" checked={step.ignoreYear !== false} onChange={e => onUpdate(step.id, { ignoreYear: e.target.checked })} />
                    Ignore Year (Repeats annually)
                  </label>
                </div>
              )}

              {/* ── Event / API Variable section ───────────────────────── */}
              {step.triggerValue === 'EVENT_API' && (
                <div style={{ marginTop: '8px', padding: '14px', background: 'rgba(99,102,241,0.06)', borderRadius: '8px', border: '1px solid rgba(99,102,241,0.25)', display: 'flex', flexDirection: 'column', gap: '10px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '2px' }}>
                    <span style={{ fontSize: '0.7rem', fontWeight: 700, color: '#6366f1', textTransform: 'uppercase', letterSpacing: '0.05em' }}>🔗 Event / API Variable Config</span>
                  </div>

                  {/* API Slug */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <label style={{ fontSize: '0.72rem', fontWeight: 600, color: '#475569' }}>Event Slug (used in API URL)</label>
                    <input
                      type="text"
                      placeholder="e.g. room-upgrade-request"
                      value={step.eventApiSlug || ''}
                      onChange={e => onUpdate(step.id, { eventApiSlug: e.target.value.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '') })}
                      style={{ padding: '6px 10px', borderRadius: '6px', border: '1px solid #c7d2fe', fontSize: '0.82rem', background: '#fff', color: '#1e293b' }}
                    />
                    {step.eventApiSlug && (
                      <div style={{ fontSize: '0.68rem', color: '#6366f1', background: '#eef2ff', padding: '4px 8px', borderRadius: '4px', fontFamily: 'monospace', wordBreak: 'break-all' }}>
                        POST /api/event-trigger/{step.eventApiSlug}
                      </div>
                    )}
                    <p style={{ fontSize: '0.68rem', color: '#64748b', margin: '2px 0 0' }}>Any external system can call this endpoint with a JSON body. Variables from the JSON will be available in your template.</p>
                  </div>

                  {/* Variable Mapping UI removed as per user request */}
                </div>
              )}

              {/* ── Informational hint for other non-date/non-API triggers ── */}
              {step.triggerValue && !step.triggerValue.startsWith('DATE_') && step.triggerValue !== 'EVENT_API' && step.triggerValue !== 'MANUAL_TRIGGER' && (
                <div style={{ marginTop: '4px', padding: '8px 10px', background: '#f0fdf4', borderRadius: '6px', border: '1px solid #bbf7d0', fontSize: '0.68rem', color: '#166534' }}>
                  💡 This trigger fires when the matching event is received via the PMS API or webhook. Standard guest fields (Name, Room, BookingReference, etc.) are automatically available as template variables.
                </div>
              )}

              {step.triggerValue === 'MANUAL_TRIGGER' && (
                <div style={{ marginTop: '4px', padding: '8px 10px', background: '#fffbeb', borderRadius: '6px', border: '1px solid #fde68a', fontSize: '0.68rem', color: '#92400e' }}>
                  ⚡ Manual trigger: this rule will only fire when explicitly called via the broadcast tool or a direct API call. Useful for one-time campaigns.
                </div>
              )}

              <div style={{ marginTop: '16px', paddingTop: '16px', borderTop: '1px dashed #e2e8f0', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                <label style={{ fontSize: '0.75rem', fontWeight: 500 }}>Audience Condition (Only trigger for...)</label>
                <select style={{ padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                  value={step.audienceFilter || 'All'}
                  onChange={e => onUpdate(step.id, { audienceFilter: e.target.value })}>
                  <option value="All">Anyone (No Filter)</option>
                  <option value="Custom">Only contacts matching Custom Column...</option>
                </select>

                {step.audienceFilter === 'Custom' && (
                  <div style={{ display: 'flex', gap: '8px', marginTop: '4px' }}>
                    <input type="text" placeholder="Column name (e.g. City)" style={{ flex: 1, padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                      value={step.audienceCustomColumn || ''} onChange={e => onUpdate(step.id, { audienceCustomColumn: e.target.value })} />
                    <span style={{ display: 'flex', alignItems: 'center', fontSize: '0.8rem', color: '#64748b' }}>=</span>
                    <input type="text" placeholder="Value (e.g. Toronto)" style={{ flex: 1, padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                      value={step.audienceCustomValue || ''} onChange={e => onUpdate(step.id, { audienceCustomValue: e.target.value })} />
                  </div>
                )}
              </div>
            </div>
          )}

          {step.stepType === 'WAIT' && (
            <div style={{ display: 'flex', gap: '0.5rem' }}>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                <label style={{ fontSize: '0.75rem', fontWeight: 500 }}>Wait Value</label>
                <input type="number" style={{ padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                  value={step.waitValue || 1}
                  onChange={e => onUpdate(step.id, { waitValue: e.target.value })} />
              </div>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                <label style={{ fontSize: '0.75rem', fontWeight: 500 }}>Unit</label>
                <select style={{ padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                  value={step.waitUnit || 'HOURS'}
                  onChange={e => onUpdate(step.id, { waitUnit: e.target.value })}>
                  <option value="MINUTES">Minutes</option>
                  <option value="HOURS">Hours</option>
                  <option value="DAYS">Days</option>
                </select>
              </div>
            </div>
          )}

          {step.stepType === 'WHATSAPP' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              <label style={{ fontSize: '0.75rem', fontWeight: 500 }}>Message Template</label>
              <select style={{ padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                value={step.templateId || ''}
                onChange={e => onUpdate(step.id, { templateId: e.target.value })}>
                <option value="">-- Select Template --</option>
                {step.templatesList?.map(t => (
                  <option key={t.Id || t.id} value={t.Id || t.id}>{t.Name || t.name}</option>
                ))}
              </select>

              {step.templateId && step.templatesList && (
                <div style={{ marginTop: '8px', padding: '12px', background: '#dcf8c6', borderRadius: '12px 12px 12px 0', alignSelf: 'flex-start', maxWidth: '90%', position: 'relative', boxShadow: '0 1px 1px rgba(0,0,0,0.1)' }}>
                  <div style={{ fontSize: '0.85rem', color: '#000', whiteSpace: 'pre-wrap', lineHeight: '1.4' }}>
                    {(() => {
                      const t = step.templatesList.find(x => (x.Id || x.id).toString() === step.templateId);
                      return t ? `Linked to Meta Template:\n${t.ExternalTemplateName || t.externalTemplateName}` : 'Template not found';
                    })()}
                  </div>
                  <div style={{ position: 'absolute', bottom: '4px', right: '8px', fontSize: '0.6rem', color: 'rgba(0,0,0,0.45)' }}>
                    Info
                  </div>
                </div>
              )}
            </div>
          )}
          
          {step.stepType === 'CONDITION' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              <label style={{ fontSize: '0.75rem', fontWeight: 500 }}>Field condition</label>
              <p style={{ fontSize: '0.7rem', color: '#64748b', margin: 0 }}>This allows splitting the journey based on contact parameters.</p>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <select style={{ flex: 1, padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                    value={step.conditionField || 'status'}
                    onChange={e => onUpdate(step.id, { conditionField: e.target.value })}>
                    <option value="status">Booking Status</option>
                    <option value="vip">Is VIP</option>
                </select>
                <input style={{ flex: 1, padding: '6px 8px', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '0.82rem' }}
                    placeholder="Value..."
                    value={step.conditionValue || ''}
                    onChange={e => onUpdate(step.id, { conditionValue: e.target.value })} />
              </div>
            </div>
          )}

          {step.stepType === 'STOP' && (
            <p style={{ fontSize: '0.75rem', color: '#64748b', margin: 0 }}>Contacts reaching this step will exit the workflow.</p>
          )}
        </div>
      )}
    </div>
  );
}

export default function AutomationBuilder({ onCancel, initialRule }) {
  const [ruleName, setRuleName] = useState(initialRule ? (initialRule.Name || initialRule.name || 'New WhatsApp Rule') : 'New WhatsApp Rule');
  const [ruleDescription, setRuleDescription] = useState(initialRule ? (initialRule.Description || initialRule.description || '') : '');
  const [dynamicHeaders, setDynamicHeaders] = React.useState([]);
  const [templates, setTemplates] = React.useState([]);
  
  const [steps, setSteps] = useState(() => {
    if (initialRule) {
      const triggerEventType = initialRule.TriggerEventType || initialRule.triggerEventType;
      const conditionsJson = initialRule.ConditionsJson || initialRule.conditionsJson;
      const isDate = triggerEventType === 'DateTrigger';
      const isEventApi = triggerEventType === 'EVENT_API';
      let triggerVal = (isDate || isEventApi) ? 'PMS_CHECKIN' : triggerEventType;
      let offsetDays = 0;
      let ignoreYear = true;
      let eventApiSlug = '';
      let eventApiVarMappings = [];

      if (isDate && conditionsJson) {
        try {
          const cond = JSON.parse(conditionsJson);
          if (cond.Column) {
            triggerVal = `DATE_${cond.Column}`;
          }
          if (cond.OffsetDays !== undefined) offsetDays = cond.OffsetDays;
          if (cond.IgnoreYear !== undefined) ignoreYear = cond.IgnoreYear;
        } catch(e) {}
      }

      if (isEventApi && conditionsJson) {
        try {
          const cond = JSON.parse(conditionsJson);
          triggerVal = 'EVENT_API';
          if (cond.EventApiSlug) eventApiSlug = cond.EventApiSlug;
          if (Array.isArray(cond.VarMappings)) eventApiVarMappings = cond.VarMappings;
        } catch(e) {}
      }

      let audienceFilter = 'All';
      let audienceCustomColumn = '';
      let audienceCustomValue = '';

      if (initialRule.AudienceFilter || initialRule.audienceFilter) {
        try {
          const af = JSON.parse(initialRule.AudienceFilter || initialRule.audienceFilter);
          if (af.Type) audienceFilter = af.Type;
          if (af.Column) audienceCustomColumn = af.Column;
          if (af.Value) audienceCustomValue = af.Value;
        } catch(e) {}
      }

      return [
        { 
          id: '1', 
          stepType: 'TRIGGER', 
          triggerValue: triggerVal, 
          stepOrder: 0,
          offsetDays,
          ignoreYear,
          eventApiSlug,
          eventApiVarMappings,
          audienceFilter,
          audienceCustomColumn,
          audienceCustomValue
        },
        { 
          id: '2', 
          stepType: 'WHATSAPP', 
          templateId: (initialRule.TemplateId || initialRule.templateId)?.toString(), 
          stepOrder: 1,
          templatesList: [] // will be populated by fetch
        },
        {
          id: '3',
          stepType: 'STOP',
          stepOrder: 2
        }
      ];
    }
    return [
      { id: '1', stepType: 'TRIGGER', triggerValue: 'PMS_CHECKIN', stepOrder: 0, audienceFilter: 'All' }
    ];
  });

  React.useEffect(() => {
    fetch('/api/contacts/columns/dates')
      .then(r => r.json())
      .then(d => {
        // Remove duplicates and case-variants (e.g. "birthday", "Birthday") by normalizing
        const uniqueKeys = new Map();
        (d || []).forEach(k => {
          const lower = k.toLowerCase();
          if (!uniqueKeys.has(lower)) {
            uniqueKeys.set(lower, k);
          }
        });
        setDynamicHeaders(Array.from(uniqueKeys.values()));
      })
      .catch(e => console.log(e));

    fetch('/api/templates')
      .then(r => r.json())
      .then(d => {
        const tpl = d?.Templates || d?.templates || (Array.isArray(d) ? d : []);
        setTemplates(tpl);
        setSteps(prev => prev.map(s => s.stepType === 'WHATSAPP' ? { ...s, templatesList: tpl } : s));
      })
      .catch(e => console.log(e));
  }, []);

  const allTriggerOptions = [
    ...TRIGGER_OPTIONS,
    ...dynamicHeaders.map(h => ({ value: `DATE_${h}`, label: `Date: ${h}`, group: 'Date Triggers' }))
  ];

  const addStep = (stepType, index) => {
    const newStep = {
      id: Date.now().toString(),
      stepType,
      stepOrder: index + 1,
      templatesList: templates
    };
    
    setSteps(prev => {
      const newSteps = [...prev];
      newSteps.splice(index + 1, 0, newStep);
      return newSteps.map((s, i) => ({ ...s, stepOrder: i }));
    });
  };

  const updateStep = (id, updates) => {
    setSteps(prev => prev.map(s => s.id === id ? { ...s, ...updates } : s));
  };

  const deleteStep = (id) => {
    setSteps(prev => {
      const newSteps = prev.filter(s => s.id !== id);
      return newSteps.map((s, i) => ({ ...s, stepOrder: i }));
    });
  };

  const saveRule = async () => {
    const triggerStep = steps.find(s => s.stepType === 'TRIGGER');
    const actionStep = steps.find(s => s.stepType === 'WHATSAPP');
    
    if (!triggerStep || !actionStep || !actionStep.templateId) {
      alert("You need at least a Trigger and a Send WhatsApp step with a selected template.");
      return;
    }

    let triggerType = triggerStep.triggerValue || 'PMS_CHECKIN';
    let conditionsJson = null;

    if (triggerType.startsWith('DATE_')) {
      const column = triggerType.replace('DATE_', '');
      triggerType = 'DateTrigger';
      conditionsJson = JSON.stringify({
        Column: column,
        OffsetDays: parseInt(triggerStep.offsetDays || 0),
        IgnoreYear: triggerStep.ignoreYear !== false
      });
    } else if (triggerType === 'EVENT_API') {
      // Persist the API slug and variable mappings into ConditionsJson
      if (!triggerStep.eventApiSlug) {
        alert('Please enter an Event Slug for the Event / API Variable trigger.');
        return;
      }
      conditionsJson = JSON.stringify({
        EventApiSlug: triggerStep.eventApiSlug,
        VarMappings: triggerStep.eventApiVarMappings || []
      });
    }

    let audienceFilterJson = null;
    if (triggerStep.audienceFilter === 'Custom') {
      audienceFilterJson = JSON.stringify({
        Type: 'Custom',
        Column: triggerStep.audienceCustomColumn || '',
        Value: triggerStep.audienceCustomValue || ''
      });
    }

    const rule = {
      Id: initialRule ? (initialRule.Id || initialRule.id) : undefined,
      Name: ruleName,
      Description: ruleDescription,
      TriggerEventType: triggerType,
      TemplateId: parseInt(actionStep.templateId),
      IsEnabled: false,
      ConditionsJson: conditionsJson,
      AudienceFilter: audienceFilterJson
    };
    
    try {
      const ruleId = initialRule ? (initialRule.Id || initialRule.id) : null;
      const res = await fetch(initialRule ? `/api/rules/${ruleId}` : '/api/rules', {
        method: initialRule ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(rule)
      });
      if (!res.ok) throw new Error();
      alert(`Saved Rule: ${ruleName}`);
      if (onCancel) {
        onCancel();
        // Little hack to force app refresh if passed via onCancel (not ideal but works for this structure)
        window.location.reload(); 
      }
    } catch (e) {
      alert('Error saving rule');
    }
  };

  return (
    <div style={{ padding: '24px', maxWidth: '1000px', margin: '0 auto', fontFamily: 'Inter, sans-serif' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', background: '#fff', padding: '16px 24px', borderRadius: '12px', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.05)' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <button onClick={onCancel} style={{ background: 'none', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', color: '#64748b' }}>
              <XCircle size={20} />
            </button>
            <input 
              type="text" 
              value={ruleName} 
              onChange={e => setRuleName(e.target.value)} 
              placeholder="Rule Name"
              style={{ fontSize: '1.25rem', fontWeight: 700, border: 'none', outline: 'none', background: 'transparent' }}
            />
            {initialRule && (
              <span style={{ 
                marginLeft: '12px', 
                padding: '4px 10px', 
                borderRadius: '999px', 
                fontSize: '0.75rem', 
                fontWeight: 600, 
                backgroundColor: initialRule.IsEnabled ? '#dcfce7' : '#fee2e2', 
                color: initialRule.IsEnabled ? '#166534' : '#991b1b' 
              }}>
                {(initialRule.IsEnabled ?? initialRule.isEnabled) ? 'Active' : 'Paused'}
              </span>
            )}
            {!initialRule && (
              <span style={{ 
                marginLeft: '12px', 
                padding: '4px 10px', 
                borderRadius: '999px', 
                fontSize: '0.75rem', 
                fontWeight: 600, 
                backgroundColor: '#f1f5f9', 
                color: '#475569' 
              }}>
                Draft
              </span>
            )}
          </div>
          <div style={{ marginLeft: '32px' }}>
            <input 
              type="text" 
              value={ruleDescription} 
              onChange={e => setRuleDescription(e.target.value)} 
              placeholder="Add a description for this automation (e.g. Sends birthday promo 7 days early)"
              style={{ fontSize: '0.85rem', color: '#64748b', border: 'none', outline: 'none', background: 'transparent', width: '400px' }}
            />
          </div>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          <button 
            className="btn btn-primary" 
            style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '8px 16px', background: '#3b82f6', color: '#fff', borderRadius: '8px', border: 'none', cursor: 'pointer', fontWeight: 500 }}
            onClick={saveRule}
          >
            <Save size={16} /> Save Rule
          </button>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', padding: '32px 0 120px 0' }}>
        {steps.map((step, index) => {
          const isStop = step.stepType === 'STOP';
          return (
            <React.Fragment key={step.id}>
              <StepCard 
                step={step} 
                onUpdate={updateStep} 
                onDelete={deleteStep} 
                canDelete={step.stepType !== 'TRIGGER'} 
                allTriggerOptions={allTriggerOptions}
              />
              {index < steps.length - 1 && !isStop && <AddBtn onAdd={(type) => addStep(type, index)} />}
            </React.Fragment>
          );
        })}
        
        {steps.length > 0 && steps[steps.length - 1].stepType !== 'STOP' && (
          <>
            <AddBtn onAdd={(type) => addStep(type, steps.length - 1)} />
            
            <div style={{
              width: 48, height: 48, borderRadius: '50%',
              border: '2px dashed #cbd5e1', display: 'flex', alignItems: 'center', justifyContent: 'center',
              color: '#cbd5e1', marginTop: '16px'
            }}>
              <CheckCircle2 size={24} />
            </div>
          </>
        )}
      </div>
    </div>
  );
}
