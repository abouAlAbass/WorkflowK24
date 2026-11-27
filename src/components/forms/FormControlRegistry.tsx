import React, { useState, useEffect, useRef, useCallback } from 'react';
import {
  Plus,
  Trash2,
  Upload,
  Download,
  FileText,
  Eye,
  ShieldCheck,
  Search,
  UserCheck,
  X,
  PenTool,
  RotateCcw,
  Database,
  Loader2,
  ChevronDown
} from 'lucide-react';
import type {
  SmartFormField,
  SmartFormFieldType,
  FormAttachmentValue,
  UserDirectoryItem
} from '../../types/smartform';
import { getWorkflowRepository } from '../../services/repositoryManager';

export const FORM_INPUT_BASE_CLASS =
  'w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-500 font-medium focus:outline-none focus:ring-2 focus:ring-[#2563EB] disabled:opacity-60 disabled:bg-slate-100 dark:disabled:bg-slate-800/70 transition-all';

export interface FormControlRenderProps {
  field: SmartFormField;
  value: unknown;
  error?: string;
  disabled: boolean;
  required: boolean;
  onChange: (value: unknown) => void;
  smartObjectData: Record<string, Record<string, unknown>[]>;
  uploadingField: string | null;
  onFileSelect: (field: SmartFormField, file: File | undefined) => Promise<void>;
  onDownloadAttachment: (field: SmartFormField, attachment: FormAttachmentValue) => Promise<void>;
  onPreviewAttachment: (attachment: FormAttachmentValue) => void;
}

export type FormControlComponent = React.FC<FormControlRenderProps>;

// ---------------------------------------------------------------------------
// Standard Built-In Controls
// ---------------------------------------------------------------------------

const TextControl: FormControlComponent = ({ field, value, error, disabled, required, onChange }) => (
  <input
    type="text"
    id={field.id}
    aria-invalid={Boolean(error)}
    aria-required={required || undefined}
    aria-describedby={error ? `${field.id}-error` : undefined}
    disabled={disabled}
    value={String(value ?? '')}
    onChange={(e) => onChange(e.target.value)}
    placeholder={field.placeholder}
    className={FORM_INPUT_BASE_CLASS}
  />
);

const TextareaControl: FormControlComponent = ({ field, value, error, disabled, required, onChange }) => (
  <textarea
    id={field.id}
    aria-invalid={Boolean(error)}
    aria-required={required || undefined}
    aria-describedby={error ? `${field.id}-error` : undefined}
    rows={3}
    disabled={disabled}
    value={String(value ?? '')}
    onChange={(e) => onChange(e.target.value)}
    placeholder={field.placeholder}
    className={FORM_INPUT_BASE_CLASS}
  />
);

const NumericControl: FormControlComponent = ({ field, value, error, disabled, required, onChange }) => (
  <div className="relative">
    {field.type === 'currency' && (
      <span className="absolute start-3.5 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">$</span>
    )}
    <input
      type="number"
      id={field.id}
      aria-invalid={Boolean(error)}
      aria-required={required || undefined}
      aria-describedby={error ? `${field.id}-error` : undefined}
      step="any"
      disabled={disabled}
      value={String(value ?? '')}
      onChange={(e) => onChange(e.target.value)}
      placeholder={field.placeholder || '0'}
      className={`${FORM_INPUT_BASE_CLASS} ${field.type === 'currency' ? 'ps-7 font-mono' : 'font-mono'}`}
    />
  </div>
);

const DateControl: FormControlComponent = ({ field, value, error, disabled, required, onChange }) => (
  <input
    type="date"
    id={field.id}
    aria-invalid={Boolean(error)}
    aria-required={required || undefined}
    aria-describedby={error ? `${field.id}-error` : undefined}
    disabled={disabled}
    value={String(value ?? '')}
    onChange={(e) => onChange(e.target.value)}
    className={FORM_INPUT_BASE_CLASS}
  />
);

const SelectControl: FormControlComponent = ({
  field,
  value,
  error,
  disabled,
  required,
  onChange,
  smartObjectData
}) => {
  let selectOptions: Array<{ value: string; label: string }> = (field.options || []).map((o) => ({
    value: o,
    label: o
  }));

  if (field.lookupSmartObjectId && smartObjectData[field.lookupSmartObjectId]) {
    const soRows = smartObjectData[field.lookupSmartObjectId];
    selectOptions = soRows.map((row) => {
      const valKey = field.lookupValueField || Object.keys(row)[0] || 'id';
      const dispKey = field.lookupDisplayField || Object.keys(row)[1] || valKey;
      return {
        value: String(row[valKey] ?? ''),
        label: String(row[dispKey] ?? row[valKey] ?? '')
      };
    });
  }

  return (
    <select
      id={field.id}
      aria-invalid={Boolean(error)}
      aria-required={required || undefined}
      aria-describedby={error ? `${field.id}-error` : undefined}
      disabled={disabled}
      value={String(value ?? '')}
      onChange={(e) => onChange(e.target.value)}
      className={FORM_INPUT_BASE_CLASS}
    >
      <option value="">— Select {field.label} —</option>
      {selectOptions.map((opt, idx) => (
        <option key={`${opt.value}-${idx}`} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  );
};

const CheckboxControl: FormControlComponent = ({ field, value, error, disabled, onChange }) => (
  <label className="flex items-center gap-2.5 p-3 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 cursor-pointer">
    <input
      type="checkbox"
      id={field.id}
      aria-invalid={Boolean(error)}
      aria-describedby={error ? `${field.id}-error` : undefined}
      disabled={disabled}
      checked={Boolean(value)}
      onChange={(e) => onChange(e.target.checked)}
      className="w-4 h-4 rounded border-slate-300 text-[#2563EB] focus:ring-[#2563EB]"
    />
    <span className="text-xs font-bold text-[#0F172A] dark:text-slate-100">{field.label}</span>
  </label>
);

const RepeaterControl: FormControlComponent = ({ field, value, disabled, onChange }) => {
  const rows = Array.isArray(value) ? (value as Record<string, unknown>[]) : [];
  const columns = field.repeaterColumns || [];

  const handleAddRow = () => {
    const emptyRow: Record<string, unknown> = {};
    columns.forEach((col) => {
      emptyRow[col.name] = '';
    });
    onChange([...rows, emptyRow]);
  };

  const handleUpdateCell = (rowIdx: number, colName: string, cellVal: string) => {
    onChange(rows.map((r, i) => (i === rowIdx ? { ...r, [colName]: cellVal } : r)));
  };

  const handleRemoveRow = (rowIdx: number) => {
    onChange(rows.filter((_, i) => i !== rowIdx));
  };

  return (
    <div className="rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-xs">
          <thead className="bg-slate-50 dark:bg-slate-800/80 text-[#475569] dark:text-slate-300 border-b border-slate-200 dark:border-slate-700">
            <tr>
              {columns.map((col) => (
                <th key={col.name} className="text-start px-3 py-2 font-bold">
                  {col.label}
                </th>
              ))}
              {!disabled && <th className="w-10 px-2 py-2" />}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
            {rows.length === 0 ? (
              <tr>
                <td
                  colSpan={(columns.length || 1) + 1}
                  className="px-3 py-4 text-center text-[#64748B] dark:text-slate-400"
                >
                  No rows added yet.
                </td>
              </tr>
            ) : (
              rows.map((row, rowIdx) => (
                <tr key={rowIdx}>
                  {columns.map((col) => (
                    <td key={col.name} className="p-2">
                      {col.type === 'select' ? (
                        <select
                          aria-label={`${col.label} row ${rowIdx + 1}`}
                          disabled={disabled}
                          value={String(row[col.name] ?? '')}
                          onChange={(e) => handleUpdateCell(rowIdx, col.name, e.target.value)}
                          className="w-full px-2.5 py-1.5 rounded-lg text-xs bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-[#0F172A] dark:text-slate-100"
                        >
                          <option value="">— {col.label} —</option>
                          {(col.options || []).map((opt, idx) => (
                            <option key={`${opt}-${idx}`} value={opt}>
                              {opt}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <input
                          type={col.type === 'number' ? 'number' : col.type === 'date' ? 'date' : 'text'}
                          aria-label={`${col.label} row ${rowIdx + 1}`}
                          disabled={disabled}
                          value={String(row[col.name] ?? '')}
                          onChange={(e) => handleUpdateCell(rowIdx, col.name, e.target.value)}
                          placeholder={col.label}
                          className="w-full px-2.5 py-1.5 rounded-lg text-xs bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700"
                        />
                      )}
                    </td>
                  ))}
                  {!disabled && (
                    <td className="p-2 text-center">
                      <button
                        type="button"
                        onClick={() => handleRemoveRow(rowIdx)}
                        aria-label="Remove row"
                        className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </td>
                  )}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      {!disabled && (
        <div className="p-2 bg-slate-50/70 dark:bg-slate-800/40 border-t border-slate-200 dark:border-slate-800">
          <button
            type="button"
            onClick={handleAddRow}
            className="px-3 py-1.5 rounded-lg text-xs font-bold text-[#2563EB] dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-950/50 flex items-center gap-1.5 cursor-pointer"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Add Line Item</span>
          </button>
        </div>
      )}
    </div>
  );
};

const FileControl: FormControlComponent = ({
  field,
  value,
  error,
  disabled,
  onChange,
  uploadingField,
  onFileSelect,
  onDownloadAttachment,
  onPreviewAttachment
}) => {
  const attachment = value && typeof value === 'object' ? (value as FormAttachmentValue) : null;

  return (
    <div className="space-y-2">
      {!disabled && !attachment && (
        <label className="flex flex-col items-center justify-center p-4 rounded-2xl border-2 border-dashed border-slate-300 dark:border-slate-700 hover:border-[#2563EB] bg-slate-50/60 dark:bg-slate-900/50 cursor-pointer transition-colors">
          <Upload className="w-5 h-5 text-[#2563EB] dark:text-blue-400 mb-1" />
          <span className="text-xs font-semibold text-[#0F172A] dark:text-slate-200">
            {uploadingField === field.name
              ? 'Scanning & Uploading...'
              : 'Click to attach PDF, PNG, JPEG, DOCX, or XLSX (max 10 MB)'}
          </span>
          <input
            type="file"
            id={field.id}
            aria-invalid={Boolean(error)}
            aria-describedby={error ? `${field.id}-error` : undefined}
            accept=".pdf,.png,.jpg,.jpeg,.docx,.xlsx"
            className="hidden"
            onChange={(e) => void onFileSelect(field, e.target.files?.[0])}
          />
        </label>
      )}

      {attachment && (
        <div className="flex items-center justify-between p-3 rounded-xl border border-emerald-300/70 dark:border-emerald-800 bg-emerald-50/40 dark:bg-emerald-950/20">
          <div className="flex items-center gap-2.5 min-w-0">
            <FileText className="w-4 h-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
            <div className="min-w-0">
              <p className="text-xs font-bold text-[#0F172A] dark:text-white truncate">{attachment.fileName}</p>
              <p className="text-[10px] text-[#64748B] dark:text-slate-400 flex items-center gap-1 font-mono">
                <ShieldCheck className="w-3 h-3 text-emerald-500" />
                {(attachment.sizeBytes / 1024).toFixed(1)} KB • Scanned
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1.5">
            {attachment.previewDataUrl && (
              <button
                type="button"
                onClick={() => onPreviewAttachment(attachment)}
                className="px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-[11px] font-bold text-[#2563EB] dark:text-blue-400 flex items-center gap-1 cursor-pointer"
              >
                <Eye className="w-3 h-3" />
                <span>Preview</span>
              </button>
            )}
            {(attachment.downloadUrl || attachment.previewDataUrl) && (
              <button
                type="button"
                onClick={() => void onDownloadAttachment(field, attachment)}
                className="px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-[11px] font-bold text-emerald-700 dark:text-emerald-400 flex items-center gap-1 cursor-pointer"
              >
                <Download className="w-3 h-3" />
                <span>Download</span>
              </button>
            )}
            {!disabled && (
              <button
                type="button"
                onClick={() => onChange(null)}
                aria-label="Remove attachment"
                className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Advanced Controls (C.3): People Picker, Signature Pad, Async Paginated Lookup
// ---------------------------------------------------------------------------

const PeoplePickerControl: FormControlComponent = ({ field, value, error, disabled, required, onChange }) => {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<UserDirectoryItem[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const selectedUser: UserDirectoryItem | null =
    value && typeof value === 'object' && 'email' in (value as Record<string, unknown>)
      ? (value as UserDirectoryItem)
      : typeof value === 'string' && value.trim()
        ? { id: value, displayName: value, email: value, department: '' }
        : null;

  useEffect(() => {
    if (!isOpen || disabled) return;
    let active = true;
    const timer = setTimeout(async () => {
      setIsLoading(true);
      try {
        const list = await getWorkflowRepository().searchUsers(query, 20);
        if (active) setResults(list);
      } catch {
        if (active) setResults([]);
      } finally {
        if (active) setIsLoading(false);
      }
    }, 220);

    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [query, isOpen, disabled]);

  if (selectedUser) {
    return (
      <div
        id={field.id}
        className="flex items-center justify-between gap-2 px-3 py-2 rounded-xl border border-blue-200 dark:border-blue-800 bg-blue-50/50 dark:bg-blue-950/30"
      >
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="w-7 h-7 rounded-full bg-[#2563EB] text-white text-[11px] font-bold flex items-center justify-center shrink-0">
            <UserCheck className="w-3.5 h-3.5" />
          </div>
          <div className="min-w-0">
            <p className="text-xs font-bold text-[#0F172A] dark:text-white truncate">{selectedUser.displayName}</p>
            <p className="text-[10px] text-[#64748B] dark:text-slate-400 truncate font-mono">
              {selectedUser.email}
              {selectedUser.department ? ` • ${selectedUser.department}` : ''}
            </p>
          </div>
        </div>
        {!disabled && (
          <button
            type="button"
            onClick={() => {
              onChange('');
              setQuery('');
            }}
            aria-label={`Clear ${field.label}`}
            className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="relative">
      <Search className="w-3.5 h-3.5 text-slate-400 absolute start-3 top-1/2 -translate-y-1/2 pointer-events-none" />
      <input
        type="text"
        id={field.id}
        role="combobox"
        aria-expanded={isOpen}
        aria-invalid={Boolean(error)}
        aria-required={required || undefined}
        aria-describedby={error ? `${field.id}-error` : undefined}
        disabled={disabled}
        value={query}
        onFocus={() => setIsOpen(true)}
        onChange={(e) => {
          setQuery(e.target.value);
          setIsOpen(true);
        }}
        placeholder={field.placeholder || 'Search people by name, email, or department...'}
        className={`${FORM_INPUT_BASE_CLASS} ps-8 pe-8`}
      />
      {isLoading && (
        <Loader2 className="w-3.5 h-3.5 text-[#2563EB] animate-spin absolute end-3 top-1/2 -translate-y-1/2" />
      )}

      {isOpen && !disabled && (
        <div
          role="listbox"
          className="absolute start-0 end-0 top-full mt-1 z-30 bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-700 rounded-xl shadow-xl max-h-56 overflow-y-auto p-1"
        >
          {results.length === 0 && !isLoading ? (
            <p className="px-3 py-2 text-[11px] text-[#64748B] dark:text-slate-400">No matching users found.</p>
          ) : (
            results.map((u) => (
              <button
                key={u.id}
                type="button"
                role="option"
                onClick={() => {
                  onChange(u);
                  setIsOpen(false);
                  setQuery('');
                }}
                className="w-full text-start px-3 py-2 rounded-lg hover:bg-blue-50 dark:hover:bg-blue-950/40 flex items-center justify-between gap-2 cursor-pointer transition-colors"
              >
                <div className="min-w-0">
                  <p className="text-xs font-bold text-[#0F172A] dark:text-white truncate">{u.displayName}</p>
                  <p className="text-[10px] text-[#64748B] dark:text-slate-400 truncate font-mono">{u.email}</p>
                </div>
                {u.department && (
                  <span className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-[#475569] dark:text-slate-300 shrink-0">
                    {u.department}
                  </span>
                )}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
};

const SignaturePadControl: FormControlComponent = ({ field, value, error, disabled, onChange }) => {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const isDrawingRef = useRef(false);
  const signatureDataUrl = typeof value === 'string' && value.startsWith('data:image/') ? value : '';

  const getPoint = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY
    };
  };

  const handlePointerDown = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (disabled) return;
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext?.('2d');
    if (!canvas || !ctx) return;
    isDrawingRef.current = true;
    const { x, y } = getPoint(e);
    ctx.strokeStyle = '#0F172A';
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.beginPath();
    ctx.moveTo(x, y);
  };

  const handlePointerMove = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!isDrawingRef.current || disabled) return;
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext?.('2d');
    if (!canvas || !ctx) return;
    const { x, y } = getPoint(e);
    ctx.lineTo(x, y);
    ctx.stroke();
  };

  const handlePointerUp = () => {
    if (!isDrawingRef.current || disabled) return;
    isDrawingRef.current = false;
    const canvas = canvasRef.current;
    if (canvas) {
      try {
        onChange(canvas.toDataURL('image/png'));
      } catch {
        // Canvas toDataURL may be stubbed in test environments
      }
    }
  };

  const handleClear = () => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext?.('2d');
    if (canvas && ctx) {
      ctx.clearRect(0, 0, canvas.width, canvas.height);
    }
    onChange('');
  };

  if (disabled) {
    return (
      <div
        id={field.id}
        className="p-3 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/60 min-h-[96px] flex items-center justify-center"
      >
        {signatureDataUrl ? (
          <img src={signatureDataUrl} alt={`${field.label} signature`} className="max-h-24 object-contain" />
        ) : (
          <span className="text-xs text-[#64748B] dark:text-slate-400">No signature captured.</span>
        )}
      </div>
    );
  }

  return (
    <div
      id={field.id}
      aria-invalid={Boolean(error)}
      aria-describedby={error ? `${field.id}-error` : undefined}
      className="rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 overflow-hidden"
    >
      <div className="px-3 py-1.5 bg-slate-50 dark:bg-slate-800/60 border-b border-slate-200 dark:border-slate-700 flex items-center justify-between">
        <span className="text-[11px] font-semibold text-[#475569] dark:text-slate-300 flex items-center gap-1.5">
          <PenTool className="w-3 h-3 text-[#2563EB]" />
          <span>Sign inside the box below</span>
        </span>
        <button
          type="button"
          onClick={handleClear}
          className="text-[11px] font-bold text-slate-500 hover:text-rose-600 flex items-center gap-1 cursor-pointer"
        >
          <RotateCcw className="w-3 h-3" />
          <span>Clear</span>
        </button>
      </div>
      <canvas
        ref={canvasRef}
        width={480}
        height={120}
        data-testid={`signature-canvas-${field.name}`}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerUp}
        onPointerLeave={handlePointerUp}
        className="w-full h-28 bg-white cursor-crosshair touch-none block"
      />
      {signatureDataUrl && (
        <div className="px-3 py-1 bg-emerald-50/70 dark:bg-emerald-950/30 border-t border-emerald-200/60 dark:border-emerald-800/60 text-[10px] font-semibold text-emerald-700 dark:text-emerald-400">
          Signature captured
        </div>
      )}
    </div>
  );
};

const AsyncLookupControl: FormControlComponent = ({
  field,
  value,
  error,
  disabled,
  required,
  onChange
}) => {
  const [query, setQuery] = useState('');
  const [items, setItems] = useState<Array<{ value: string; label: string }>>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const selectedLabel =
    value && typeof value === 'object' && 'label' in (value as Record<string, unknown>)
      ? String((value as { label: unknown }).label ?? '')
      : String(value ?? '');

  const mapRows = useCallback(
    (rows: Record<string, unknown>[]) =>
      rows.map((row) => {
        const keys = Object.keys(row);
        const valKey = field.lookupValueField || keys[0] || 'id';
        const dispKey = field.lookupDisplayField || keys[1] || valKey;
        return {
          value: String(row[valKey] ?? ''),
          label: String(row[dispKey] ?? row[valKey] ?? '')
        };
      }),
    [field.lookupDisplayField, field.lookupValueField]
  );

  const fetchPage = useCallback(
    async (searchText: string, pageNum: number, append: boolean) => {
      if (!field.lookupSmartObjectId) {
        setItems([]);
        setHasMore(false);
        setTotalCount(0);
        return;
      }
      setIsLoading(true);
      try {
        const res = await getWorkflowRepository().lookupSmartObject(
          field.lookupSmartObjectId,
          searchText,
          pageNum,
          25
        );
        const mapped = mapRows(res.items);
        setItems((prev) => (append ? [...prev, ...mapped] : mapped));
        setPage(res.page);
        setHasMore(res.hasMore);
        setTotalCount(res.totalCount);
      } catch {
        if (!append) setItems([]);
        setHasMore(false);
      } finally {
        setIsLoading(false);
      }
    },
    [field.lookupSmartObjectId, mapRows]
  );

  useEffect(() => {
    if (!isOpen || disabled) return;
    const timer = setTimeout(() => {
      void fetchPage(query, 1, false);
    }, 250);
    return () => clearTimeout(timer);
  }, [query, isOpen, disabled, fetchPage]);

  if (selectedLabel) {
    return (
      <div
        id={field.id}
        className="flex items-center justify-between gap-2 px-3 py-2 rounded-xl border border-indigo-200 dark:border-indigo-800 bg-indigo-50/40 dark:bg-indigo-950/30"
      >
        <div className="flex items-center gap-2 min-w-0">
          <Database className="w-3.5 h-3.5 text-indigo-600 dark:text-indigo-400 shrink-0" />
          <span className="text-xs font-bold text-[#0F172A] dark:text-white truncate">{selectedLabel}</span>
        </div>
        {!disabled && (
          <button
            type="button"
            onClick={() => {
              onChange('');
              setQuery('');
            }}
            aria-label={`Clear ${field.label}`}
            className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="relative">
      <Search className="w-3.5 h-3.5 text-slate-400 absolute start-3 top-1/2 -translate-y-1/2 pointer-events-none" />
      <input
        type="text"
        id={field.id}
        role="combobox"
        aria-expanded={isOpen}
        aria-invalid={Boolean(error)}
        aria-required={required || undefined}
        aria-describedby={error ? `${field.id}-error` : undefined}
        disabled={disabled}
        value={query}
        onFocus={() => setIsOpen(true)}
        onChange={(e) => {
          setQuery(e.target.value);
          setIsOpen(true);
        }}
        placeholder={field.placeholder || `Search ${field.label}...`}
        className={`${FORM_INPUT_BASE_CLASS} ps-8 pe-8`}
      />
      {isLoading ? (
        <Loader2 className="w-3.5 h-3.5 text-[#2563EB] animate-spin absolute end-3 top-1/2 -translate-y-1/2" />
      ) : (
        <ChevronDown className="w-3.5 h-3.5 text-slate-400 absolute end-3 top-1/2 -translate-y-1/2 pointer-events-none" />
      )}

      {isOpen && !disabled && (
        <div
          role="listbox"
          className="absolute start-0 end-0 top-full mt-1 z-30 bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-700 rounded-xl shadow-xl max-h-60 overflow-y-auto p-1"
        >
          {items.length === 0 && !isLoading ? (
            <p className="px-3 py-2 text-[11px] text-[#64748B] dark:text-slate-400">No matching records found.</p>
          ) : (
            <>
              {items.map((opt, idx) => (
                <button
                  key={`${opt.value}-${idx}`}
                  type="button"
                  role="option"
                  onClick={() => {
                    onChange({ value: opt.value, label: opt.label });
                    setIsOpen(false);
                    setQuery('');
                  }}
                  className="w-full text-start px-3 py-2 rounded-lg hover:bg-indigo-50 dark:hover:bg-indigo-950/40 flex items-center justify-between gap-2 cursor-pointer transition-colors"
                >
                  <span className="text-xs font-semibold text-[#0F172A] dark:text-white truncate">{opt.label}</span>
                  <span className="text-[10px] font-mono text-[#64748B] dark:text-slate-400 shrink-0">{opt.value}</span>
                </button>
              ))}
              {hasMore && (
                <button
                  type="button"
                  disabled={isLoading}
                  onClick={() => void fetchPage(query, page + 1, true)}
                  className="w-full mt-1 px-3 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800 hover:bg-slate-200 dark:hover:bg-slate-700 text-[11px] font-bold text-[#2563EB] dark:text-blue-400 cursor-pointer"
                >
                  {isLoading ? 'Loading...' : `Load more (${items.length} / ${totalCount})`}
                </button>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Open/Closed Control Registry
// ---------------------------------------------------------------------------

class ControlRegistry {
  private readonly controls = new Map<string, FormControlComponent>();

  constructor() {
    this.register('text', TextControl);
    this.register('textarea', TextareaControl);
    this.register('number', NumericControl);
    this.register('currency', NumericControl);
    this.register('date', DateControl);
    this.register('select', SelectControl);
    this.register('checkbox', CheckboxControl);
    this.register('repeater', RepeaterControl);
    this.register('file', FileControl);
    this.register('peoplePicker', PeoplePickerControl);
    this.register('signature', SignaturePadControl);
    this.register('lookup', AsyncLookupControl);
  }

  register(type: SmartFormFieldType | string, component: FormControlComponent): void {
    this.controls.set(type, component);
  }

  get(type: SmartFormFieldType | string): FormControlComponent | undefined {
    return this.controls.get(type);
  }

  has(type: SmartFormFieldType | string): boolean {
    return this.controls.has(type);
  }

  getRegisteredTypes(): string[] {
    return Array.from(this.controls.keys());
  }
}

export const FormControlRegistry = new ControlRegistry();
