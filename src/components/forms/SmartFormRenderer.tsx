import React, { useState, useEffect, useRef, useMemo } from 'react';
import {
  FileText,
  AlertCircle,
  CheckCircle2,
  X,
  Calculator,
  Save
} from 'lucide-react';
import type {
  SmartFormDefinition,
  SmartFormField,
  SmartFormVisibilityRule,
  FormAttachmentValue
} from '../../types/smartform';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore, generateIdempotencyKey } from '../../store/useSmartFormStore';
import {
  evaluateLegacyVisibilityRule,
  evaluateSmartFormState
} from '../../utils/jsonLogicEngine';
import { FormControlRegistry } from './FormControlRegistry';

interface SmartFormRendererProps {
  form: SmartFormDefinition;
  initialValues?: Record<string, unknown>;
  forceReadOnly?: boolean;
  submitLabel?: string;
  draftLabel?: string;
  hideSubmitButton?: boolean;
  onChangeValues?: (values: Record<string, unknown>) => void;
  onSaveDraft?: (values: Record<string, unknown>) => Promise<void> | void;
  onSubmit?: (
    values: Record<string, unknown>,
    meta?: { idempotencyKey: string }
  ) => Promise<void> | void;
}

export function evaluateFieldVisibility(
  rule: SmartFormVisibilityRule | null | undefined,
  values: Record<string, unknown>
): boolean {
  return evaluateLegacyVisibilityRule(rule, values);
}

export const SmartFormRenderer: React.FC<SmartFormRendererProps> = ({
  form,
  initialValues,
  forceReadOnly = false,
  submitLabel = 'Submit Form & Start Process',
  draftLabel = 'Save Draft',
  hideSubmitButton = false,
  onChangeValues,
  onSaveDraft,
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
  const [isSavingDraft, setIsSavingDraft] = useState(false);
  // One Idempotency-Key per logical submission attempt: reused across retries until the server accepts it
  const idempotencyKeyRef = useRef<string>(generateIdempotencyKey());

  // Isomorphic JSONLogic rule evaluation (visible, required, readOnly, computedValue in DAG order)
  const ruleEval = useMemo(
    () => evaluateSmartFormState(form.fields, form.rules, values),
    [form.fields, form.rules, values]
  );

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
    const evaluatedNext = evaluateSmartFormState(form.fields, form.rules, next);
    onChangeValues?.(evaluatedNext.effectiveValues);
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

  const handleDownloadAttachment = async (field: SmartFormField, attachment: FormAttachmentValue) => {
    try {
      await downloadAttachment(attachment);
    } catch (err: unknown) {
      setErrors((prev) => ({
        ...prev,
        [field.name]: err instanceof Error ? err.message : 'Attachment download failed'
      }));
    }
  };

  const buildSanitizedVisibleValues = (): Record<string, unknown> => {
    const sanitizedVisibleValues: Record<string, unknown> = {};
    for (const f of form.fields) {
      if (f.type === 'section') continue;
      if (!ruleEval.visibleFields.has(f.name)) continue;
      sanitizedVisibleValues[f.name] = ruleEval.effectiveValues[f.name];
    }
    return sanitizedVisibleValues;
  };

  const handleSaveDraftClick = async () => {
    if (!onSaveDraft || forceReadOnly) return;
    setIsSavingDraft(true);
    setSubmitError(null);
    try {
      await onSaveDraft(buildSanitizedVisibleValues());
    } catch (err: unknown) {
      setSubmitError(err instanceof Error ? err.message : 'Failed to save draft.');
    } finally {
      setIsSavingDraft(false);
    }
  };

  const handleFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (ruleEval.hasCycle) {
      setSubmitError(`CYCLIC_FORM_RULE: Circular rule dependency detected (${ruleEval.cyclePath.join(' -> ')}).`);
      return;
    }

    const nextErrors: Record<string, string> = {};

    for (const f of form.fields) {
      if (f.type === 'section' || forceReadOnly) continue;
      if (!ruleEval.visibleFields.has(f.name)) continue;
      if (ruleEval.readOnlyFields.has(f.name) || ruleEval.computedFields.has(f.name)) continue;
      if (!ruleEval.requiredFields.has(f.name)) continue;

      const v = ruleEval.effectiveValues[f.name];
      if (f.type === 'repeater') {
        if (!Array.isArray(v) || v.length === 0) {
          nextErrors[f.name] = `${f.label} is required`;
        }
      } else if (v === undefined || v === null || (typeof v === 'string' && v.trim() === '')) {
        nextErrors[f.name] = `${f.label} is required`;
      }
    }

    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    // Strip hidden phantom values so only visible declared fields are submitted
    const sanitizedVisibleValues = buildSanitizedVisibleValues();

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

  return (
    <form onSubmit={handleFormSubmit} className="space-y-5">
      {ruleEval.hasCycle && (
        <div
          role="alert"
          className="p-3 rounded-xl border border-rose-300 dark:border-rose-800 bg-rose-50/70 dark:bg-rose-950/30 text-xs font-semibold text-rose-700 dark:text-rose-300 flex items-center gap-2"
        >
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{`CYCLIC_FORM_RULE: Circular dependency detected (${ruleEval.cyclePath.join(' -> ')}).`}</span>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {form.fields.map((field) => {
          const isVisible = field.type === 'section' || ruleEval.visibleFields.has(field.name);
          if (!isVisible) return null;

          const isComputed = ruleEval.computedFields.has(field.name);
          const isFieldRequired = ruleEval.requiredFields.has(field.name);
          const isFieldReadOnly = ruleEval.readOnlyFields.has(field.name);
          const isDisabled = forceReadOnly || isFieldReadOnly || isComputed;
          const hasRulesForField = Boolean(
            field.visibilityRule ||
            (form.rules && form.rules.some((r) => r.targetField.toLowerCase() === field.name.toLowerCase()))
          );
          const colSpanClass =
            field.width === 'half' &&
            field.type !== 'section' &&
            field.type !== 'repeater' &&
            field.type !== 'signature'
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

          const ControlComponent = FormControlRegistry.get(field.type);
          const currentVal = ruleEval.effectiveValues[field.name];

          return (
            <div key={field.id} className={`${colSpanClass} space-y-1.5`}>
              {field.type !== 'checkbox' && (
                <label
                  htmlFor={field.type === 'repeater' ? undefined : field.id}
                  className="text-xs font-bold text-[#0F172A] dark:text-slate-200 flex items-center justify-between"
                >
                  <span>
                    {field.label}
                    {isFieldRequired && !isDisabled && <span className="text-rose-500 ms-1">*</span>}
                  </span>
                  <span className="flex items-center gap-1">
                    {isComputed && (
                      <span className="text-[9px] font-mono px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 inline-flex items-center gap-1">
                        <Calculator className="w-2.5 h-2.5" />
                        Calculated
                      </span>
                    )}
                    {!isComputed && hasRulesForField && (
                      <span className="text-[9px] font-mono px-1.5 py-0.5 rounded bg-purple-500/10 text-purple-600 dark:text-purple-400">
                        Conditional
                      </span>
                    )}
                  </span>
                </label>
              )}

              {ControlComponent ? (
                <ControlComponent
                  field={field}
                  value={currentVal}
                  error={errors[field.name]}
                  disabled={isDisabled}
                  required={isFieldRequired}
                  onChange={(val) => updateValue(field.name, val)}
                  smartObjectData={smartObjectData}
                  uploadingField={uploadingField}
                  onFileSelect={handleFileSelect}
                  onDownloadAttachment={handleDownloadAttachment}
                  onPreviewAttachment={setPreviewAttachment}
                />
              ) : (
                <div className="p-2.5 rounded-xl border border-amber-300 dark:border-amber-800 bg-amber-50/50 dark:bg-amber-950/20 text-xs text-amber-800 dark:text-amber-300">
                  Unsupported control type: <code className="font-mono">{field.type}</code>
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
        <div className="pt-4 border-t border-slate-200 dark:border-slate-800 flex items-center justify-end gap-2.5">
          {onSaveDraft && (
            <button
              type="button"
              disabled={isSavingDraft || isSubmitting}
              onClick={() => void handleSaveDraftClick()}
              className="px-4 py-2.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 hover:bg-slate-50 dark:hover:bg-slate-700 text-[#0F172A] dark:text-slate-100 text-xs font-bold flex items-center gap-2 cursor-pointer disabled:opacity-50 transition-all"
            >
              <Save className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
              <span>{isSavingDraft ? 'Saving Draft...' : draftLabel}</span>
            </button>
          )}
          <button
            type="submit"
            disabled={isSubmitting || isSavingDraft}
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
