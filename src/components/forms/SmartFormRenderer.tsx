import React, { useState, useEffect, useRef } from 'react';
import {
  Plus,
  Trash2,
  Upload,
  Download,
  FileText,
  Eye,
  ShieldCheck,
  AlertCircle,
  CheckCircle2,
  X
} from 'lucide-react';
import type {
  SmartFormDefinition,
  SmartFormField,
  SmartFormVisibilityRule,
  FormAttachmentValue
} from '../../types/smartform';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore, generateIdempotencyKey } from '../../store/useSmartFormStore';

interface SmartFormRendererProps {
  form: SmartFormDefinition;
  initialValues?: Record<string, unknown>;
  forceReadOnly?: boolean;
  submitLabel?: string;
  hideSubmitButton?: boolean;
  onChangeValues?: (values: Record<string, unknown>) => void;
  onSubmit?: (
    values: Record<string, unknown>,
    meta?: { idempotencyKey: string }
  ) => Promise<void> | void;
}

export function evaluateFieldVisibility(
  rule: SmartFormVisibilityRule | null | undefined,
  values: Record<string, unknown>
): boolean {
  if (!rule || !rule.field) return true;

  const rawActual = values[rule.field];
  const actualStr = rawActual === undefined || rawActual === null ? '' : String(rawActual);
  const expectedStr = rule.value ?? '';

  if (rule.operator === 'is_not_empty') {
    return actualStr.trim().length > 0;
  }

  const actualNum = Number(actualStr);
  const expectedNum = Number(expectedStr);
  const bothNumeric = actualStr.trim() !== '' && expectedStr.trim() !== '' && !Number.isNaN(actualNum) && !Number.isNaN(expectedNum);

  switch (rule.operator) {
    case 'greater_than':
      return bothNumeric ? actualNum > expectedNum : actualStr > expectedStr;
    case 'less_than':
      return bothNumeric ? actualNum < expectedNum : actualStr < expectedStr;
    case 'equals':
      return bothNumeric ? actualNum === expectedNum : actualStr.toLowerCase() === expectedStr.toLowerCase();
    case 'not_equals':
      return bothNumeric ? actualNum !== expectedNum : actualStr.toLowerCase() !== expectedStr.toLowerCase();
    case 'contains':
      return actualStr.toLowerCase().includes(expectedStr.toLowerCase());
    default:
      return true;
  }
}

export const SmartFormRenderer: React.FC<SmartFormRendererProps> = ({
  form,
  initialValues,
  forceReadOnly = false,
  submitLabel = 'Submit Form & Start Process',
  hideSubmitButton = false,
  onChangeValues,
  onSubmit
}) => {
  const { smartObjectData } = useWorkflowStore();
  const uploadAttachment = useSmartFormStore((s) => s.uploadAttachment);
  const downloadAttachment = useSmartFormStore((s) => s.downloadAttachment);

  const buildInitialMap = (): Record<string, unknown> => {
    const map: Record<string, unknown> = {};
    for (const f of form.fields) {
      if (f.type === 'section') continue;
      if (initialValues && f.name in initialValues) {
        map[f.name] = initialValues[f.name];
      } else if (f.type === 'checkbox') {
        map[f.name] = f.defaultValue === 'true';
      } else if (f.type === 'repeater') {
        map[f.name] = [];
      } else {
        map[f.name] = f.defaultValue ?? '';
      }
    }
    return map;
  };

  const [values, setValues] = useState<Record<string, unknown>>(buildInitialMap);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [uploadingField, setUploadingField] = useState<string | null>(null);
  const [previewAttachment, setPreviewAttachment] = useState<FormAttachmentValue | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // One Idempotency-Key per logical submission attempt: reused across retries until the server accepts it
  const idempotencyKeyRef = useRef<string>(generateIdempotencyKey());

  useEffect(() => {
    setValues(buildInitialMap());
    setErrors({});
    setSubmitError(null);
    idempotencyKeyRef.current = generateIdempotencyKey();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form.id]);

  const updateValue = (fieldName: string, val: unknown) => {
    const next = { ...values, [fieldName]: val };
    setValues(next);
    if (errors[fieldName]) {
      setErrors((prev) => {
        const copy = { ...prev };
        delete copy[fieldName];
        return copy;
      });
    }
    onChangeValues?.(next);
  };

  const handleFileSelect = async (field: SmartFormField, file: File | undefined) => {
    if (!file) return;
    setUploadingField(field.name);
    try {
      const uploaded = await uploadAttachment(file);
      updateValue(field.name, uploaded);
    } catch (err: unknown) {
      setErrors((prev) => ({
        ...prev,
        [field.name]: err instanceof Error ? err.message : 'Attachment upload failed'
      }));
    } finally {
      setUploadingField(null);
    }
  };

  const handleAddRepeaterRow = (field: SmartFormField) => {
    const currentRows = Array.isArray(values[field.name]) ? (values[field.name] as Record<string, unknown>[]) : [];
    const emptyRow: Record<string, unknown> = {};
    (field.repeaterColumns || []).forEach((col) => {
      emptyRow[col.name] = '';
    });
    updateValue(field.name, [...currentRows, emptyRow]);
  };

  const handleUpdateRepeaterCell = (field: SmartFormField, rowIdx: number, colName: string, cellVal: string) => {
    const currentRows = Array.isArray(values[field.name]) ? (values[field.name] as Record<string, unknown>[]) : [];
    const nextRows = currentRows.map((r, i) => (i === rowIdx ? { ...r, [colName]: cellVal } : r));
    updateValue(field.name, nextRows);
  };

  const handleRemoveRepeaterRow = (field: SmartFormField, rowIdx: number) => {
    const currentRows = Array.isArray(values[field.name]) ? (values[field.name] as Record<string, unknown>[]) : [];
    updateValue(field.name, currentRows.filter((_, i) => i !== rowIdx));
  };

  const handleFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const nextErrors: Record<string, string> = {};

    for (const f of form.fields) {
      if (f.type === 'section' || !f.required || f.readOnly || forceReadOnly) continue;
      if (!evaluateFieldVisibility(f.visibilityRule, values)) continue;

      const v = values[f.name];
      if (v === undefined || v === null || String(v).trim() === '') {
        nextErrors[f.name] = `${f.label} is required`;
      }
    }

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    // Strip hidden phantom values so only visible declared fields are submitted
    const sanitizedVisibleValues: Record<string, unknown> = {};
    for (const f of form.fields) {
      if (f.type === 'section') continue;
      if (!evaluateFieldVisibility(f.visibilityRule, values)) continue;
      sanitizedVisibleValues[f.name] = values[f.name];
    }

    if (onSubmit) {
      setIsSubmitting(true);
      setSubmitError(null);
      try {
        await onSubmit(sanitizedVisibleValues, { idempotencyKey: idempotencyKeyRef.current });
        // Accepted by the server: the next submission is a new logical attempt
        idempotencyKeyRef.current = generateIdempotencyKey();
      } catch (err: unknown) {
        const maybeFieldErrors = (err as { fieldErrors?: Record<string, string> })?.fieldErrors;
        if (maybeFieldErrors && Object.keys(maybeFieldErrors).length > 0) {
          setErrors((prev) => ({ ...prev, ...maybeFieldErrors }));
        }
        setSubmitError(err instanceof Error ? err.message : 'Form submission failed.');
      } finally {
        setIsSubmitting(false);
      }
    }
  };

  const inputBase =
    'w-full px-3.5 py-2.5 rounded-xl text-xs bg-white dark:bg-slate-900 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 placeholder-[#64748B] dark:placeholder-slate-500 font-medium focus:outline-none focus:ring-2 focus:ring-[#2563EB] disabled:opacity-60 disabled:bg-slate-100 dark:disabled:bg-slate-800/70 transition-all';

  return (
    <form onSubmit={handleFormSubmit} className="space-y-5">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {form.fields.map((field) => {
          const isVisible = evaluateFieldVisibility(field.visibilityRule, values);
          if (!isVisible) return null;

          const isDisabled = forceReadOnly || field.readOnly;
          const colSpanClass = field.width === 'half' && field.type !== 'section' && field.type !== 'repeater'
            ? 'md:col-span-1'
            : 'md:col-span-2';

          if (field.type === 'section') {
            return (
              <div key={field.id} className="md:col-span-2 pt-3 pb-1 border-b border-slate-200 dark:border-slate-800">
                <h4 className="text-sm font-extrabold text-[#0F172A] dark:text-white">{field.label}</h4>
                {field.helpText && (
                  <p className="text-[11px] text-[#64748B] dark:text-slate-400 mt-0.5">{field.helpText}</p>
                )}
              </div>
            );
          }

          // Dynamic SmartObject lookup options if configured
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

          const currentVal = values[field.name];
          const attachment = (field.type === 'file' && currentVal && typeof currentVal === 'object')
            ? (currentVal as FormAttachmentValue)
            : null;
          const repeaterRows = field.type === 'repeater' && Array.isArray(currentVal)
            ? (currentVal as Record<string, unknown>[])
            : [];

          return (
            <div key={field.id} className={`${colSpanClass} space-y-1.5`}>
              {field.type !== 'checkbox' && (
                <label
                  htmlFor={field.type === 'repeater' ? undefined : field.id}
                  className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center justify-between"
                >
                  <span>
                    {field.label}
                    {field.required && !isDisabled && <span className="text-rose-500 ms-1">*</span>}
                  </span>
                  {field.visibilityRule && (
                    <span className="text-[9px] font-mono px-1.5 py-0.5 rounded bg-purple-500/10 text-purple-600 dark:text-purple-400">
                      Conditional
                    </span>
                  )}
                </label>
              )}

              {field.type === 'text' && (
                <input
                  type="text"
                  id={field.id}
                  aria-invalid={Boolean(errors[field.name])}
                  aria-required={field.required || undefined}
                  aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                  disabled={isDisabled}
                  value={String(currentVal ?? '')}
                  onChange={(e) => updateValue(field.name, e.target.value)}
                  placeholder={field.placeholder}
                  className={inputBase}
                />
              )}

              {field.type === 'textarea' && (
                <textarea
                  id={field.id}
                  aria-invalid={Boolean(errors[field.name])}
                  aria-required={field.required || undefined}
                  aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                  rows={3}
                  disabled={isDisabled}
                  value={String(currentVal ?? '')}
                  onChange={(e) => updateValue(field.name, e.target.value)}
                  placeholder={field.placeholder}
                  className={inputBase}
                />
              )}

              {(field.type === 'number' || field.type === 'currency') && (
                <div className="relative">
                  {field.type === 'currency' && (
                    <span className="absolute start-3.5 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">$</span>
                  )}
                  <input
                    type="number"
                    id={field.id}
                    aria-invalid={Boolean(errors[field.name])}
                    aria-required={field.required || undefined}
                    aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                    step="any"
                    disabled={isDisabled}
                    value={String(currentVal ?? '')}
                    onChange={(e) => updateValue(field.name, e.target.value)}
                    placeholder={field.placeholder || '0'}
                    className={`${inputBase} ${field.type === 'currency' ? 'ps-7 font-mono' : 'font-mono'}`}
                  />
                </div>
              )}

              {field.type === 'date' && (
                <input
                  type="date"
                  id={field.id}
                  aria-invalid={Boolean(errors[field.name])}
                  aria-required={field.required || undefined}
                  aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                  disabled={isDisabled}
                  value={String(currentVal ?? '')}
                  onChange={(e) => updateValue(field.name, e.target.value)}
                  className={inputBase}
                />
              )}

              {field.type === 'select' && (
                <select
                  id={field.id}
                  aria-invalid={Boolean(errors[field.name])}
                  aria-required={field.required || undefined}
                  aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                  disabled={isDisabled}
                  value={String(currentVal ?? '')}
                  onChange={(e) => updateValue(field.name, e.target.value)}
                  className={inputBase}
                >
                  <option value="">— Select {field.label} —</option>
                  {selectOptions.map((opt, idx) => (
                    <option key={`${opt.value}-${idx}`} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              )}

              {field.type === 'checkbox' && (
                <label className="flex items-center gap-2.5 p-3 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 cursor-pointer">
                  <input
                    type="checkbox"
                    id={field.id}
                    aria-invalid={Boolean(errors[field.name])}
                    aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                    disabled={isDisabled}
                    checked={Boolean(currentVal)}
                    onChange={(e) => updateValue(field.name, e.target.checked)}
                    className="w-4 h-4 rounded border-slate-300 text-[#2563EB] focus:ring-[#2563EB]"
                  />
                  <span className="text-xs font-bold text-[#0F172A] dark:text-slate-100">{field.label}</span>
                </label>
              )}

              {field.type === 'repeater' && (
                <div className="rounded-2xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 overflow-hidden">
                  <div className="overflow-x-auto">
                    <table className="w-full text-xs">
                      <thead className="bg-slate-50 dark:bg-slate-800/80 text-[#475569] dark:text-slate-300 border-b border-slate-200 dark:border-slate-700">
                        <tr>
                          {(field.repeaterColumns || []).map((col) => (
                            <th key={col.name} className="text-start px-3 py-2 font-bold">
                              {col.label}
                            </th>
                          ))}
                          {!isDisabled && <th className="w-10 px-2 py-2" />}
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                        {repeaterRows.length === 0 ? (
                          <tr>
                            <td
                              colSpan={(field.repeaterColumns?.length || 1) + 1}
                              className="px-3 py-4 text-center text-[#64748B] dark:text-slate-400"
                            >
                              No rows added yet.
                            </td>
                          </tr>
                        ) : (
                          repeaterRows.map((row, rowIdx) => (
                            <tr key={rowIdx}>
                              {(field.repeaterColumns || []).map((col) => (
                                <td key={col.name} className="p-2">
                                  <input
                                    type={col.type === 'number' ? 'number' : col.type === 'date' ? 'date' : 'text'}
                                    disabled={isDisabled}
                                    value={String(row[col.name] ?? '')}
                                    onChange={(e) => handleUpdateRepeaterCell(field, rowIdx, col.name, e.target.value)}
                                    placeholder={col.label}
                                    className="w-full px-2.5 py-1.5 rounded-lg text-xs bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700"
                                  />
                                </td>
                              ))}
                              {!isDisabled && (
                                <td className="p-2 text-center">
                                  <button
                                    type="button"
                                    onClick={() => handleRemoveRepeaterRow(field, rowIdx)}
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
                  {!isDisabled && (
                    <div className="p-2 bg-slate-50/70 dark:bg-slate-800/40 border-t border-slate-200 dark:border-slate-800">
                      <button
                        type="button"
                        onClick={() => handleAddRepeaterRow(field)}
                        className="px-3 py-1.5 rounded-lg text-xs font-bold text-[#2563EB] dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-950/50 flex items-center gap-1.5 cursor-pointer"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        <span>Add Line Item</span>
                      </button>
                    </div>
                  )}
                </div>
              )}

              {field.type === 'file' && (
                <div className="space-y-2">
                  {!isDisabled && !attachment && (
                    <label className="flex flex-col items-center justify-center p-4 rounded-2xl border-2 border-dashed border-slate-300 dark:border-slate-700 hover:border-[#2563EB] bg-slate-50/60 dark:bg-slate-900/50 cursor-pointer transition-colors">
                      <Upload className="w-5 h-5 text-[#2563EB] dark:text-blue-400 mb-1" />
                      <span className="text-xs font-semibold text-[#0F172A] dark:text-slate-200">
                        {uploadingField === field.name ? 'Scanning & Uploading...' : 'Click to attach PDF, PNG, JPEG, DOCX, or XLSX (max 10 MB)'}
                      </span>
                      <input
                        type="file"
                        id={field.id}
                        aria-invalid={Boolean(errors[field.name])}
                        aria-describedby={errors[field.name] ? `${field.id}-error` : undefined}
                        accept=".pdf,.png,.jpg,.jpeg,.docx,.xlsx"
                        className="hidden"
                        onChange={(e) => handleFileSelect(field, e.target.files?.[0])}
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
                            onClick={() => setPreviewAttachment(attachment)}
                            className="px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-[11px] font-bold text-[#2563EB] dark:text-blue-400 flex items-center gap-1 cursor-pointer"
                          >
                            <Eye className="w-3 h-3" />
                            <span>Preview</span>
                          </button>
                        )}
                        {(attachment.downloadUrl || attachment.previewDataUrl) && (
                          <button
                            type="button"
                            onClick={async () => {
                              try {
                                await downloadAttachment(attachment);
                              } catch (err: unknown) {
                                setErrors((prev) => ({
                                  ...prev,
                                  [field.name]: err instanceof Error ? err.message : 'Attachment download failed'
                                }));
                              }
                            }}
                            className="px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-[11px] font-bold text-emerald-700 dark:text-emerald-400 flex items-center gap-1 cursor-pointer"
                          >
                            <Download className="w-3 h-3" />
                            <span>Download</span>
                          </button>
                        )}
                        {!isDisabled && (
                          <button
                            type="button"
                            onClick={() => updateValue(field.name, null)}
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
              )}

              {field.helpText && (
                <p className="text-[10px] text-[#64748B] dark:text-slate-400">{field.helpText}</p>
              )}

              {errors[field.name] && (
                <p
                  id={`${field.id}-error`}
                  role="alert"
                  className="text-[11px] font-semibold text-rose-600 dark:text-rose-400 flex items-center gap-1"
                >
                  <AlertCircle className="w-3.5 h-3.5" />
                  <span>{errors[field.name]}</span>
                </p>
              )}
            </div>
          );
        })}
      </div>

      {submitError && (
        <div
          role="alert"
          className="p-3 rounded-xl border border-rose-300 dark:border-rose-800 bg-rose-50/70 dark:bg-rose-950/30 text-xs font-semibold text-rose-700 dark:text-rose-300 flex items-center gap-2"
        >
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{submitError}</span>
        </div>
      )}

      {!hideSubmitButton && !forceReadOnly && (
        <div className="pt-4 border-t border-slate-200 dark:border-slate-800 flex justify-end">
          <button
            type="submit"
            disabled={isSubmitting}
            className="px-5 py-2.5 rounded-xl bg-[#2563EB] hover:bg-blue-700 text-white text-xs font-bold shadow-md flex items-center gap-2 cursor-pointer disabled:opacity-50 transition-all"
          >
            <CheckCircle2 className="w-4 h-4" />
            <span>{isSubmitting ? 'Submitting...' : submitLabel}</span>
          </button>
        </div>
      )}

      {/* Attachment Preview Modal (PDF & Images) */}
      {previewAttachment && (
        <div className="fixed inset-0 z-50 bg-slate-950/70 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 rounded-2xl max-w-3xl w-full max-h-[85vh] flex flex-col overflow-hidden shadow-2xl">
            <div className="p-3.5 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between">
              <span className="text-xs font-bold text-[#0F172A] dark:text-white truncate">
                {previewAttachment.fileName}
              </span>
              <button
                type="button"
                onClick={() => setPreviewAttachment(null)}
                className="p-1 text-slate-400 hover:text-slate-700 dark:hover:text-white cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="flex-1 overflow-auto p-4 bg-slate-100 dark:bg-slate-950 flex items-center justify-center min-h-[360px]">
              {previewAttachment.contentType.startsWith('image/') ? (
                <img
                  src={previewAttachment.previewDataUrl || previewAttachment.downloadUrl}
                  alt={previewAttachment.fileName}
                  className="max-h-[65vh] rounded-lg shadow"
                />
              ) : previewAttachment.contentType === 'application/pdf' ? (
                <iframe
                  title={previewAttachment.fileName}
                  src={previewAttachment.previewDataUrl || previewAttachment.downloadUrl}
                  className="w-full h-[65vh] rounded-lg border-0 bg-white"
                />
              ) : (
                <div className="text-center space-y-2 p-6">
                  <FileText className="w-10 h-10 text-blue-500 mx-auto" />
                  <p className="text-xs font-semibold text-[#0F172A] dark:text-slate-200">
                    {previewAttachment.fileName} ({(previewAttachment.sizeBytes / 1024).toFixed(1)} KB)
                  </p>
                  {previewAttachment.sha256Hash && (
                    <p className="text-[10px] font-mono text-slate-500">SHA-256: {previewAttachment.sha256Hash}</p>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </form>
  );
};
