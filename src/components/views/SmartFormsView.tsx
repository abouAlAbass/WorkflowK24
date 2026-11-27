import React, { useState, useEffect } from 'react';
import {
  FileSpreadsheet,
  Plus,
  Sparkles,
  Trash2,
  ArrowUp,
  ArrowDown,
  Save,
  Eye,
  PenTool,
  Database,
  Workflow,
  GitBranch,
  CheckCircle2,
  Type,
  AlignLeft,
  Hash,
  DollarSign,
  Calendar,
  CheckSquare,
  ListFilter,
  Table,
  Upload,
  LayoutTemplate,
  X,
  Send,
  History,
  Lock
} from 'lucide-react';
import { useSmartFormStore } from '../../store/useSmartFormStore';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { SmartFormRenderer } from '../forms/SmartFormRenderer';
import { FormRulesEditor } from '../forms/FormRulesEditor';
import type {
  SmartFormField,
  SmartFormFieldType,
  VisibilityOperator
} from '../../types/smartform';

const PALETTE_ITEMS: Array<{
  type: SmartFormFieldType;
  label: string;
  icon: React.ReactNode;
}> = [
  { type: 'section', label: 'Section Header', icon: <LayoutTemplate className="w-3.5 h-3.5 text-slate-500" /> },
  { type: 'text', label: 'Text Input', icon: <Type className="w-3.5 h-3.5 text-blue-500" /> },
  { type: 'textarea', label: 'Multi-line Text', icon: <AlignLeft className="w-3.5 h-3.5 text-indigo-500" /> },
  { type: 'number', label: 'Number', icon: <Hash className="w-3.5 h-3.5 text-emerald-500" /> },
  { type: 'currency', label: 'Currency ($)', icon: <DollarSign className="w-3.5 h-3.5 text-emerald-600" /> },
  { type: 'date', label: 'Date Picker', icon: <Calendar className="w-3.5 h-3.5 text-amber-500" /> },
  { type: 'checkbox', label: 'Boolean Checkbox', icon: <CheckSquare className="w-3.5 h-3.5 text-teal-500" /> },
  { type: 'select', label: 'Dropdown / Lookup', icon: <ListFilter className="w-3.5 h-3.5 text-purple-500" /> },
  { type: 'repeater', label: 'Repeatable Table', icon: <Table className="w-3.5 h-3.5 text-cyan-600" /> },
  { type: 'file', label: 'File / PDF Upload', icon: <Upload className="w-3.5 h-3.5 text-rose-500" /> }
];

export const SmartFormsView: React.FC = () => {
  const {
    forms,
    selectedFormId,
    selectedFieldId,
    selectedVersionId,
    versionsByForm,
    versionDetails,
    selectForm,
    selectField,
    selectVersion,
    fetchFormVersions,
    publishFormVersion,
    createForm,
    saveForm,
    deleteForm,
    generateFromSmartObject,
    addFieldToActiveForm,
    updateFieldInActiveForm,
    removeFieldFromActiveForm,
    moveFieldInActiveForm,
    updateActiveFormMeta,
    addRuleToActiveForm,
    updateRuleInActiveForm,
    removeRuleFromActiveForm,
    submitForm
  } = useSmartFormStore();

  const { smartObjects, savedWorkflows, syncDataFromRepository } = useWorkflowStore();

  const [mode, setMode] = useState<'design' | 'preview'>('design');
  const [savedBanner, setSavedBanner] = useState<string | null>(null);
  const [errorBanner, setErrorBanner] = useState<string | null>(null);
  const [isPublishing, setIsPublishing] = useState(false);
  const [isGenerateModalOpen, setIsGenerateModalOpen] = useState(false);
  const [isNewFormModalOpen, setIsNewFormModalOpen] = useState(false);
  const [targetSOId, setTargetSOId] = useState(smartObjects[0]?.id || '');
  const [targetWfKey, setTargetWfKey] = useState('');

  const [newFormTitle, setNewFormTitle] = useState('');
  const [newFormCategory, setNewFormCategory] = useState('General');
  const [draggedIdx, setDraggedIdx] = useState<number | null>(null);

  const activeForm = forms.find((f) => f.id === selectedFormId) || forms[0];
  const formVersions = activeForm ? (versionsByForm[activeForm.id] || []) : [];
  const inspectedVersion = activeForm && selectedVersionId
    ? versionDetails[`${activeForm.id}:${selectedVersionId}`] || null
    : null;
  const isViewingHistoricalVersion = Boolean(inspectedVersion && inspectedVersion.status !== 'Draft');
  const displayedFields = isViewingHistoricalVersion && inspectedVersion
    ? inspectedVersion.fields
    : (activeForm?.fields || []);
  const displayedRules = isViewingHistoricalVersion && inspectedVersion
    ? (inspectedVersion.rules || [])
    : (activeForm?.rules || []);
  const selectedField = displayedFields.find((fld) => fld.id === selectedFieldId) || null;

  const currentVersionNumber = inspectedVersion?.versionNumber ?? activeForm?.latestVersionNumber ?? 1;
  const currentVersionStatus = inspectedVersion?.status ?? activeForm?.latestVersionStatus ?? activeForm?.status ?? 'Draft';
  const canPublish = Boolean(
    activeForm &&
    !isViewingHistoricalVersion &&
    (activeForm.hasUnpublishedDraft || activeForm.latestVersionStatus === 'Draft' || activeForm.status === 'Draft')
  );

  useEffect(() => {
    if (activeForm?.id) {
      void fetchFormVersions(activeForm.id);
    }
  }, [activeForm?.id, fetchFormVersions]);

  const flashBanner = (msg: string) => {
    setSavedBanner(msg);
    setTimeout(() => setSavedBanner(null), 3000);
  };

  const handleQuickAddField = (type: SmartFormFieldType) => {
    if (!activeForm) return;
    const count = activeForm.fields.length + 1;
    const newField: SmartFormField = {
      id: `fld-${Date.now().toString(36)}`,
      name: type === 'section' ? `Section_${count}` : `Field_${count}`,
      label: type === 'section' ? 'New Form Section' : `New ${type.charAt(0).toUpperCase() + type.slice(1)} Field`,
      type,
      required: false,
      readOnly: false,
      width: type === 'section' || type === 'textarea' || type === 'repeater' || type === 'file' ? 'full' : 'half',
      placeholder: type === 'text' || type === 'textarea' ? 'Enter value...' : undefined,
      options: type === 'select' ? ['Option A', 'Option B', 'Option C'] : undefined,
      repeaterColumns:
        type === 'repeater'
          ? [
              { name: 'Description', label: 'Description', type: 'text', required: true },
              { name: 'Amount', label: 'Amount', type: 'number', required: true }
            ]
          : undefined
    };
    addFieldToActiveForm(newField);
  };

  const handleGenerateFromSO = async (e: React.FormEvent) => {
    e.preventDefault();
    const so = smartObjects.find((s) => s.id === targetSOId) || smartObjects[0];
    if (!so) return;
    const generated = await generateFromSmartObject(so, targetWfKey || undefined);
    setIsGenerateModalOpen(false);
    setMode('design');
    flashBanner(`SmartForm "${generated.title}" generated from ${so.displayName}!`);
  };

  const handleCreateBlankForm = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newFormTitle.trim()) return;
    await createForm({
      name: newFormTitle.trim().replace(/\s+/g, ''),
      title: newFormTitle.trim(),
      description: 'Custom No-Code SmartForm',
      category: newFormCategory,
      linkedSmartObjectId: null,
      linkedWorkflowKey: null
    });
    setNewFormTitle('');
    setIsNewFormModalOpen(false);
    setMode('design');
  };

  const handleSaveActiveForm = async () => {
    if (!activeForm || isViewingHistoricalVersion) return;
    try {
      await saveForm(activeForm);
      setErrorBanner(null);
      flashBanner(`Saved draft of "${activeForm.title}"!`);
    } catch (err: unknown) {
      setErrorBanner(err instanceof Error ? err.message : 'Failed to save the form.');
      setTimeout(() => setErrorBanner(null), 8000);
    }
  };

  const handlePublishVersion = async () => {
    if (!activeForm || !canPublish) return;
    setIsPublishing(true);
    try {
      const published = await publishFormVersion(activeForm.id);
      setErrorBanner(null);
      flashBanner(`Published v${published.latestVersionNumber ?? currentVersionNumber} of "${published.title}"!`);
    } catch (err: unknown) {
      setErrorBanner(err instanceof Error ? err.message : 'Failed to publish form version.');
      setTimeout(() => setErrorBanner(null), 8000);
    } finally {
      setIsPublishing(false);
    }
  };

  const handleLiveSubmit = async (
    values: Record<string, unknown>,
    meta?: { idempotencyKey: string }
  ) => {
    if (!activeForm) return;
    const sub = await submitForm(activeForm.id, values, meta?.idempotencyKey);
    await syncDataFromRepository();
    flashBanner(
      sub.linkedInstanceId
        ? `Submitted (${sub.id}) & started workflow instance ${sub.linkedInstanceId}!`
        : `Form submitted successfully (${sub.id})!`
    );
  };

  const inputClass =
    'w-full px-3 py-2 rounded-xl text-xs bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-[#0F172A] dark:text-slate-100 font-semibold focus:outline-none focus:ring-2 focus:ring-[#2563EB]';

  const lookupSO = smartObjects.find((s) => s.id === selectedField?.lookupSmartObjectId);

  return (
    <div className="flex-1 flex overflow-hidden select-none">
      {/* Left Column: Forms Registry & Drag-and-Drop Field Palette */}
      <aside className="w-72 bg-white dark:bg-[#111827] border-e border-[#E2E8F0] dark:border-[#1F2937] flex flex-col overflow-hidden">
        <div className="p-3.5 border-b border-[#E2E8F0] dark:border-[#1F2937] space-y-2">
          <div className="flex items-center justify-between">
            <h2 className="text-xs font-extrabold uppercase tracking-wider text-[#0F172A] dark:text-white flex items-center gap-1.5">
              <FileSpreadsheet className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
              <span>SmartForms ({forms.length})</span>
            </h2>
            <button
              type="button"
              onClick={() => setIsNewFormModalOpen(true)}
              data-testid="new-smartform-btn"
              className="px-2 py-1 rounded-lg bg-[#2563EB] hover:bg-blue-700 text-white text-[11px] font-bold flex items-center gap-1 cursor-pointer"
            >
              <Plus className="w-3 h-3" />
              <span>New</span>
            </button>
          </div>

          <button
            type="button"
            onClick={() => {
              setTargetSOId(smartObjects[0]?.id || '');
              setIsGenerateModalOpen(true);
            }}
            data-testid="generate-from-so-btn"
            className="w-full py-2 px-3 rounded-xl bg-gradient-to-r from-indigo-600 to-blue-600 hover:from-indigo-500 hover:to-blue-500 text-white text-xs font-bold flex items-center justify-center gap-1.5 shadow-xs cursor-pointer transition-all"
          >
            <Sparkles className="w-3.5 h-3.5" />
            <span>Generate from SmartObject</span>
          </button>
        </div>

        {/* Forms List */}
        <div className="p-2.5 space-y-1.5 max-h-52 overflow-y-auto border-b border-[#E2E8F0] dark:border-[#1F2937]">
          {forms.map((f) => {
            const isSelected = activeForm?.id === f.id;
            return (
              <div
                key={f.id}
                onClick={() => selectForm(f.id)}
                className={`p-2.5 rounded-xl border text-xs cursor-pointer transition-all flex items-center justify-between ${
                  isSelected
                    ? 'border-[#2563EB] bg-blue-50/70 dark:bg-blue-950/40 text-[#0F172A] dark:text-white font-bold'
                    : 'border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/60 text-[#475569] dark:text-slate-300'
                }`}
              >
                <div className="min-w-0 flex-1">
                  <p className="truncate font-bold">{f.title}</p>
                  <p className="text-[10px] text-[#64748B] dark:text-slate-400 truncate">
                    {f.fields.length} controls • {f.category}
                  </p>
                </div>
                {forms.length > 1 && (
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      deleteForm(f.id);
                    }}
                    aria-label={`Delete form ${f.title}`}
                    className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>
            );
          })}
        </div>

        {/* Drag & Drop Control Toolbox */}
        <div className="flex-1 overflow-y-auto p-3 space-y-2">
          <span className="text-[10px] font-extrabold uppercase tracking-wider text-[#64748B] dark:text-slate-400 block">
            Form Controls Toolbox
          </span>
          <div className="grid grid-cols-1 gap-1.5">
            {PALETTE_ITEMS.map((item) => (
              <button
                key={item.type}
                type="button"
                draggable
                onDragStart={(e) => e.dataTransfer.setData('application/tamam-form-field', item.type)}
                onClick={() => handleQuickAddField(item.type)}
                data-testid={`palette-add-${item.type}`}
                className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-[#F8FAFC] dark:bg-slate-900/70 hover:border-[#2563EB] text-xs font-semibold text-[#0F172A] dark:text-slate-200 flex items-center justify-between cursor-pointer transition-all"
              >
                <span className="flex items-center gap-2">
                  {item.icon}
                  <span>{item.label}</span>
                </span>
                <Plus className="w-3.5 h-3.5 text-slate-400" />
              </button>
            ))}
          </div>
        </div>
      </aside>

      {/* Center: Form Canvas / Live Interactive Preview */}
      <section className="flex-1 flex flex-col overflow-hidden bg-[#F1F5F9] dark:bg-[#0B0F19]">
        {activeForm && (
          <>
            {/* Studio Toolbar */}
            <div className="h-14 px-5 bg-white dark:bg-[#111827] border-b border-[#E2E8F0] dark:border-[#1F2937] flex items-center justify-between gap-4">
              <div className="flex items-center gap-3 min-w-0">
                <input
                  type="text"
                  value={activeForm.title}
                  disabled={isViewingHistoricalVersion}
                  onChange={(e) => updateActiveFormMeta({ title: e.target.value })}
                  aria-label="Form Title"
                  className="text-sm font-extrabold bg-transparent border-b border-transparent hover:border-slate-300 focus:border-[#2563EB] focus:outline-none text-[#0F172A] dark:text-white truncate disabled:opacity-70"
                />
                <span
                  data-testid="smartform-version-badge"
                  className={`text-[10px] font-mono px-2.5 py-0.5 rounded-full font-bold whitespace-nowrap ${
                    currentVersionStatus === 'Published'
                      ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                      : currentVersionStatus === 'Archived'
                        ? 'bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                        : 'bg-amber-500/15 text-amber-700 dark:text-amber-400'
                  }`}
                >
                  {`v${currentVersionNumber} • ${currentVersionStatus}`}
                </span>

                {formVersions.length > 0 && (
                  <div className="flex items-center gap-1.5">
                    <History className="w-3.5 h-3.5 text-slate-400" />
                    <label htmlFor="smartform-version-history" className="sr-only">
                      Version History
                    </label>
                    <select
                      id="smartform-version-history"
                      data-testid="smartform-version-selector"
                      value={selectedVersionId || ''}
                      onChange={(e) => selectVersion(e.target.value || null)}
                      className="px-2 py-1 rounded-lg bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-[11px] font-semibold text-[#0F172A] dark:text-slate-200"
                    >
                      <option value="">
                        {`Latest (v${activeForm.latestVersionNumber ?? 1} • ${activeForm.latestVersionStatus ?? activeForm.status})`}
                      </option>
                      {formVersions.map((v) => (
                        <option key={v.id} value={v.id}>
                          {`v${v.versionNumber} • ${v.status}${v.publishedBy ? ` (${v.publishedBy})` : ''}`}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
              </div>

              <div className="flex items-center gap-2">
                {errorBanner && (
                  <span
                    role="alert"
                    className="text-xs font-bold text-rose-700 dark:text-rose-300 bg-rose-50 dark:bg-rose-950/50 px-3 py-1 rounded-xl max-w-md truncate"
                    title={errorBanner}
                  >
                    {errorBanner}
                  </span>
                )}

                {savedBanner && (
                  <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-1 bg-emerald-50 dark:bg-emerald-950/50 px-3 py-1 rounded-xl">
                    <CheckCircle2 className="w-3.5 h-3.5" />
                    <span>{savedBanner}</span>
                  </span>
                )}

                <div className="inline-flex rounded-xl border border-slate-200 dark:border-slate-800 p-0.5 bg-slate-100 dark:bg-slate-900">
                  <button
                    type="button"
                    onClick={() => setMode('design')}
                    data-testid="form-mode-design"
                    className={`px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 cursor-pointer transition-all ${
                      mode === 'design'
                        ? 'bg-white dark:bg-slate-800 text-[#2563EB] dark:text-blue-400 shadow-2xs'
                        : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200'
                    }`}
                  >
                    <PenTool className="w-3.5 h-3.5" />
                    <span>Designer</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setMode('preview')}
                    data-testid="form-mode-preview"
                    className={`px-3 py-1.5 rounded-lg text-xs font-bold flex items-center gap-1.5 cursor-pointer transition-all ${
                      mode === 'preview'
                        ? 'bg-white dark:bg-slate-800 text-[#2563EB] dark:text-blue-400 shadow-2xs'
                        : 'text-slate-500 hover:text-slate-800 dark:hover:text-slate-200'
                    }`}
                  >
                    <Eye className="w-3.5 h-3.5" />
                    <span>Live Preview & Run</span>
                  </button>
                </div>

                <button
                  type="button"
                  onClick={handleSaveActiveForm}
                  disabled={isViewingHistoricalVersion}
                  data-testid="save-smartform-btn"
                  className="px-3.5 py-2 rounded-xl bg-[#2563EB] hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-bold flex items-center gap-1.5 shadow-xs cursor-pointer"
                >
                  <Save className="w-3.5 h-3.5" />
                  <span>Save Form</span>
                </button>

                <button
                  type="button"
                  onClick={handlePublishVersion}
                  disabled={!canPublish || isPublishing}
                  data-testid="publish-smartform-btn"
                  className="px-3.5 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 text-white text-xs font-bold flex items-center gap-1.5 shadow-xs cursor-pointer"
                >
                  <Send className="w-3.5 h-3.5" />
                  <span>{isPublishing ? 'Publishing...' : 'Publish Version'}</span>
                </button>
              </div>
            </div>

            {isViewingHistoricalVersion && inspectedVersion && (
              <div
                data-testid="smartform-readonly-version-banner"
                className="px-5 py-2 bg-amber-50 dark:bg-amber-950/40 border-b border-amber-200 dark:border-amber-900 flex items-center justify-between text-xs text-amber-800 dark:text-amber-300 font-semibold"
              >
                <span className="flex items-center gap-2">
                  <Lock className="w-3.5 h-3.5" />
                  <span>
                    {`Viewing immutable snapshot v${inspectedVersion.versionNumber} (${inspectedVersion.status}) in read-only mode.`}
                  </span>
                </span>
                <button
                  type="button"
                  onClick={() => selectVersion(null)}
                  className="px-2.5 py-1 rounded-lg bg-amber-600 hover:bg-amber-700 text-white text-[11px] font-bold cursor-pointer"
                >
                  Return to Latest
                </button>
              </div>
            )}

            {/* Bindings Bar (SmartObject + Workflow Link) */}
            <div className="px-5 py-2.5 bg-slate-50 dark:bg-slate-900/70 border-b border-[#E2E8F0] dark:border-[#1F2937] flex flex-wrap items-center gap-4 text-xs">
              <div className="flex items-center gap-2">
                <Database className="w-3.5 h-3.5 text-indigo-500" />
                <span className="font-bold text-[#475569] dark:text-slate-300">Bound SmartObject:</span>
                <select
                  value={activeForm.linkedSmartObjectId || ''}
                  onChange={(e) => updateActiveFormMeta({ linkedSmartObjectId: e.target.value || null })}
                  className="px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-xs font-semibold"
                >
                  <option value="">— Unbound (Standalone Form) —</option>
                  {smartObjects.map((so) => (
                    <option key={so.id} value={so.id}>
                      {so.displayName} ({so.name})
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex items-center gap-2">
                <Workflow className="w-3.5 h-3.5 text-emerald-500" />
                <span className="font-bold text-[#475569] dark:text-slate-300">Starts Workflow:</span>
                <select
                  value={activeForm.linkedWorkflowKey || ''}
                  onChange={(e) => updateActiveFormMeta({ linkedWorkflowKey: e.target.value || null })}
                  className="px-2.5 py-1 rounded-lg bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-700 text-xs font-semibold"
                >
                  <option value="">— None —</option>
                  <option value="onboarding">Employee Onboarding Process</option>
                  <option value="expense">Executive Purchase Request</option>
                  <option value="itAccess">IT System Access Request</option>
                  {savedWorkflows.map((wf) => (
                    <option key={wf.id} value={wf.id}>
                      {wf.name} ({wf.id})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {/* Canvas Body */}
            <div
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault();
                const droppedType = e.dataTransfer.getData('application/tamam-form-field') as SmartFormFieldType;
                if (droppedType) {
                  handleQuickAddField(droppedType);
                }
              }}
              className="flex-1 overflow-y-auto p-6"
            >
              <div className="max-w-3xl mx-auto bg-white dark:bg-[#111827] border border-[#E2E8F0] dark:border-[#1F2937] rounded-3xl shadow-sm p-6 space-y-5">
                <div className="pb-4 border-b border-slate-200 dark:border-slate-800">
                  <h3 className="text-lg font-extrabold text-[#0F172A] dark:text-white">{activeForm.title}</h3>
                  <p className="text-xs text-[#64748B] dark:text-slate-400 mt-1">{activeForm.description}</p>
                </div>

                {mode === 'preview' ? (
                  <SmartFormRenderer
                    form={
                      isViewingHistoricalVersion
                        ? { ...activeForm, fields: displayedFields, rules: displayedRules }
                        : activeForm
                    }
                    forceReadOnly={isViewingHistoricalVersion}
                    hideSubmitButton={isViewingHistoricalVersion}
                    submitLabel={
                      activeForm.linkedWorkflowKey
                        ? `Submit & Start "${activeForm.linkedWorkflowKey}" Workflow`
                        : 'Submit Form Record'
                    }
                    onSubmit={handleLiveSubmit}
                  />
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    {displayedFields.map((fld, idx) => {
                      const isSelected = selectedFieldId === fld.id;
                      const colSpan =
                        fld.width === 'half' && fld.type !== 'section' && fld.type !== 'repeater'
                          ? 'md:col-span-1'
                          : 'md:col-span-2';

                      return (
                        <div
                          key={fld.id}
                          draggable
                          onDragStart={() => setDraggedIdx(idx)}
                          onDragOver={(e) => e.preventDefault()}
                          onDrop={(e) => {
                            e.stopPropagation();
                            if (draggedIdx !== null && draggedIdx !== idx) {
                              moveFieldInActiveForm(draggedIdx, idx);
                            }
                            setDraggedIdx(null);
                          }}
                          onClick={() => selectField(fld.id)}
                          className={`${colSpan} p-3.5 rounded-2xl border transition-all cursor-pointer ${
                            isSelected
                              ? 'border-[#2563EB] bg-blue-50/40 dark:bg-blue-950/30 ring-2 ring-blue-500/20'
                              : 'border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 bg-[#F8FAFC] dark:bg-slate-900/60'
                          }`}
                        >
                          <div className="flex items-center justify-between gap-2">
                            <div className="flex items-center gap-2 min-w-0">
                              <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-slate-200/80 dark:bg-slate-800 text-[#475569] dark:text-slate-300 font-bold">
                                {fld.type}
                              </span>
                              <span className="text-xs font-extrabold text-[#0F172A] dark:text-white truncate">
                                {fld.label}
                              </span>
                              {fld.required && <span className="text-rose-500 font-bold text-xs">*</span>}
                            </div>

                            <div className="flex items-center gap-1 shrink-0">
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  moveFieldInActiveForm(idx, idx - 1);
                                }}
                                disabled={idx === 0}
                                aria-label="Move field up"
                                className="p-1 text-slate-400 hover:text-slate-700 disabled:opacity-30 cursor-pointer"
                              >
                                <ArrowUp className="w-3.5 h-3.5" />
                              </button>
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  moveFieldInActiveForm(idx, idx + 1);
                                }}
                                disabled={idx === activeForm.fields.length - 1}
                                aria-label="Move field down"
                                className="p-1 text-slate-400 hover:text-slate-700 disabled:opacity-30 cursor-pointer"
                              >
                                <ArrowDown className="w-3.5 h-3.5" />
                              </button>
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  removeFieldFromActiveForm(fld.id);
                                }}
                                aria-label={`Remove field ${fld.label}`}
                                className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </div>
                          </div>

                          <div className="mt-2 flex flex-wrap items-center gap-1.5 text-[10px]">
                            <span className="font-mono text-[#64748B] dark:text-slate-400">{`{${fld.name}}`}</span>
                            <span className="px-1.5 py-0.5 rounded bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-slate-500">
                              {fld.width === 'half' ? '50% Width' : '100% Width'}
                            </span>
                            {fld.lookupSmartObjectId && (
                              <span className="px-1.5 py-0.5 rounded bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 font-semibold">
                                SmartObject Lookup
                              </span>
                            )}
                            {fld.visibilityRule && (
                              <span className="px-1.5 py-0.5 rounded bg-purple-500/10 text-purple-600 dark:text-purple-400 font-semibold">
                                Show if {fld.visibilityRule.field} {fld.visibilityRule.operator} {fld.visibilityRule.value}
                              </span>
                            )}
                            {displayedRules
                              .filter((r) => r.targetField.toLowerCase() === fld.name.toLowerCase())
                              .map((r) => (
                                <span
                                  key={r.id}
                                  className={`px-1.5 py-0.5 rounded font-semibold ${
                                    r.effect === 'computedValue'
                                      ? 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                                      : 'bg-purple-500/10 text-purple-600 dark:text-purple-400'
                                  }`}
                                >
                                  {r.effect}
                                </span>
                              ))}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>
          </>
        )}
      </section>

      {/* Right Column: Field Property & Conditional Visibility Rule Inspector */}
      {mode === 'design' && (
        <aside className="w-80 bg-white dark:bg-[#111827] border-s border-[#E2E8F0] dark:border-[#1F2937] overflow-y-auto p-4 space-y-4">
          {!selectedField ? (
            <div className="text-center py-12 text-xs text-[#64748B] dark:text-slate-400">
              Select a control on the form canvas to configure its properties, SmartObject binding, and visibility rules.
            </div>
          ) : (
            <>
              <div className="pb-2.5 border-b border-slate-200 dark:border-slate-800">
                <span className="text-[10px] font-mono uppercase text-[#2563EB] dark:text-blue-400 font-bold">
                  Control Inspector ({selectedField.type})
                </span>
                <h4 className="text-sm font-extrabold text-[#0F172A] dark:text-white truncate">
                  {selectedField.label}
                </h4>
              </div>

              <div className="space-y-3">
                <div>
                  <label className="text-[11px] font-bold block mb-1">Display Label</label>
                  <input
                    type="text"
                    value={selectedField.label}
                    onChange={(e) => updateFieldInActiveForm(selectedField.id, { label: e.target.value })}
                    className={inputClass}
                  />
                </div>

                {selectedField.type !== 'section' && (
                  <div>
                    <label className="text-[11px] font-bold block mb-1">Variable / SmartObject Field Name</label>
                    <input
                      type="text"
                      value={selectedField.name}
                      onChange={(e) =>
                        updateFieldInActiveForm(selectedField.id, { name: e.target.value.replace(/\s+/g, '_') })
                      }
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                )}

                {selectedField.type !== 'section' && selectedField.type !== 'repeater' && (
                  <div>
                    <label className="text-[11px] font-bold block mb-1">Grid Column Width</label>
                    <div className="grid grid-cols-2 gap-2">
                      <button
                        type="button"
                        onClick={() => updateFieldInActiveForm(selectedField.id, { width: 'half' })}
                        className={`py-1.5 rounded-lg text-xs font-bold border cursor-pointer ${
                          selectedField.width === 'half'
                            ? 'border-[#2563EB] bg-blue-500/10 text-[#2563EB]'
                            : 'border-slate-300 dark:border-slate-700'
                        }`}
                      >
                        Half (50%)
                      </button>
                      <button
                        type="button"
                        onClick={() => updateFieldInActiveForm(selectedField.id, { width: 'full' })}
                        className={`py-1.5 rounded-lg text-xs font-bold border cursor-pointer ${
                          selectedField.width === 'full'
                            ? 'border-[#2563EB] bg-blue-500/10 text-[#2563EB]'
                            : 'border-slate-300 dark:border-slate-700'
                        }`}
                      >
                        Full (100%)
                      </button>
                    </div>
                  </div>
                )}

                {selectedField.type !== 'section' && (
                  <div className="flex items-center gap-4 pt-1">
                    <label className="flex items-center gap-1.5 text-xs font-bold cursor-pointer">
                      <input
                        type="checkbox"
                        checked={selectedField.required}
                        onChange={(e) => updateFieldInActiveForm(selectedField.id, { required: e.target.checked })}
                      />
                      <span>Required</span>
                    </label>
                    <label className="flex items-center gap-1.5 text-xs font-bold cursor-pointer">
                      <input
                        type="checkbox"
                        checked={selectedField.readOnly}
                        onChange={(e) => updateFieldInActiveForm(selectedField.id, { readOnly: e.target.checked })}
                      />
                      <span>Read-Only</span>
                    </label>
                  </div>
                )}

                {(selectedField.type === 'text' ||
                  selectedField.type === 'textarea' ||
                  selectedField.type === 'number' ||
                  selectedField.type === 'currency') && (
                  <div>
                    <label className="text-[11px] font-bold block mb-1">Default Value</label>
                    <input
                      type="text"
                      value={selectedField.defaultValue || ''}
                      onChange={(e) => updateFieldInActiveForm(selectedField.id, { defaultValue: e.target.value })}
                      className={inputClass}
                    />
                  </div>
                )}

                <div>
                  <label className="text-[11px] font-bold block mb-1">Help Text / Subtitle</label>
                  <input
                    type="text"
                    value={selectedField.helpText || ''}
                    onChange={(e) => updateFieldInActiveForm(selectedField.id, { helpText: e.target.value })}
                    className={inputClass}
                    placeholder="Optional guidance below control"
                  />
                </div>

                {/* Dropdown Options OR SmartObject Data Lookup */}
                {selectedField.type === 'select' && (
                  <div className="p-3 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/60 space-y-2.5">
                    <span className="text-[11px] font-extrabold text-[#0F172A] dark:text-white block">
                      Dropdown Data Source
                    </span>
                    <div>
                      <label className="text-[10px] font-bold text-slate-500 block mb-1">
                        Bind to SmartObject (Dynamic Lookup)
                      </label>
                      <select
                        value={selectedField.lookupSmartObjectId || ''}
                        onChange={(e) => {
                          const soId = e.target.value || undefined;
                          const target = smartObjects.find((s) => s.id === soId);
                          updateFieldInActiveForm(selectedField.id, {
                            lookupSmartObjectId: soId,
                            lookupValueField: target?.fields[0]?.name,
                            lookupDisplayField: target?.fields[1]?.name || target?.fields[0]?.name
                          });
                        }}
                        className={inputClass}
                      >
                        <option value="">— Use Static List Below —</option>
                        {smartObjects.map((so) => (
                          <option key={so.id} value={so.id}>
                            {so.displayName}
                          </option>
                        ))}
                      </select>
                    </div>

                    {lookupSO ? (
                      <div className="grid grid-cols-2 gap-2">
                        <div>
                          <label className="text-[10px] font-bold text-slate-500 block mb-1">Value Field</label>
                          <select
                            value={selectedField.lookupValueField || ''}
                            onChange={(e) =>
                              updateFieldInActiveForm(selectedField.id, { lookupValueField: e.target.value })
                            }
                            className={inputClass}
                          >
                            {lookupSO.fields.map((f) => (
                              <option key={f.name} value={f.name}>
                                {f.name}
                              </option>
                            ))}
                          </select>
                        </div>
                        <div>
                          <label className="text-[10px] font-bold text-slate-500 block mb-1">Display Field</label>
                          <select
                            value={selectedField.lookupDisplayField || ''}
                            onChange={(e) =>
                              updateFieldInActiveForm(selectedField.id, { lookupDisplayField: e.target.value })
                            }
                            className={inputClass}
                          >
                            {lookupSO.fields.map((f) => (
                              <option key={f.name} value={f.name}>
                                {f.name}
                              </option>
                            ))}
                          </select>
                        </div>
                      </div>
                    ) : (
                      <div>
                        <label className="text-[10px] font-bold text-slate-500 block mb-1">
                          Static Options (comma-separated)
                        </label>
                        <input
                          type="text"
                          value={(selectedField.options || []).join(', ')}
                          onChange={(e) =>
                            updateFieldInActiveForm(selectedField.id, {
                              options: e.target.value
                                .split(',')
                                .map((s) => s.trim())
                                .filter(Boolean)
                            })
                          }
                          className={inputClass}
                        />
                      </div>
                    )}
                  </div>
                )}

                {/* Declarative JSONLogic Rules Editor (visible / required / readOnly / computedValue) */}
                {selectedField.type !== 'section' && activeForm && (
                  <FormRulesEditor
                    selectedField={selectedField}
                    allFields={displayedFields}
                    allRules={displayedRules}
                    disabled={isViewingHistoricalVersion}
                    onAddRule={addRuleToActiveForm}
                    onUpdateRule={updateRuleInActiveForm}
                    onRemoveRule={removeRuleFromActiveForm}
                  />
                )}

                {/* Legacy Conditional Visibility Rule (Backward Compatible) */}
                <div className="p-3 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/60 space-y-2.5">
                  <div className="flex items-center justify-between">
                    <span className="text-[11px] font-extrabold flex items-center gap-1.5">
                      <GitBranch className="w-3.5 h-3.5 text-purple-500" />
                      <span>Conditional Visibility</span>
                    </span>
                    <input
                      type="checkbox"
                      checked={Boolean(selectedField.visibilityRule)}
                      onChange={(e) =>
                        updateFieldInActiveForm(selectedField.id, {
                          visibilityRule: e.target.checked
                            ? {
                                field:
                                  activeForm.fields.find((f) => f.id !== selectedField.id && f.type !== 'section')
                                    ?.name || 'Amount',
                                operator: 'greater_than',
                                value: '5000'
                              }
                            : null
                        })
                      }
                    />
                  </div>

                  {selectedField.visibilityRule && (
                    <div className="space-y-2">
                      <select
                        value={selectedField.visibilityRule.field}
                        onChange={(e) =>
                          updateFieldInActiveForm(selectedField.id, {
                            visibilityRule: { ...selectedField.visibilityRule!, field: e.target.value }
                          })
                        }
                        className={inputClass}
                      >
                        {activeForm.fields
                          .filter((f) => f.id !== selectedField.id && f.type !== 'section')
                          .map((f) => (
                            <option key={f.id} value={f.name}>
                              {f.label} ({f.name})
                            </option>
                          ))}
                      </select>

                      <select
                        value={selectedField.visibilityRule.operator}
                        onChange={(e) =>
                          updateFieldInActiveForm(selectedField.id, {
                            visibilityRule: {
                              ...selectedField.visibilityRule!,
                              operator: e.target.value as VisibilityOperator
                            }
                          })
                        }
                        className={inputClass}
                      >
                        <option value="equals">Equals (==)</option>
                        <option value="not_equals">Not Equals (!=)</option>
                        <option value="greater_than">Greater Than (&gt;)</option>
                        <option value="less_than">Less Than (&lt;)</option>
                        <option value="contains">Contains</option>
                        <option value="is_not_empty">Is Not Empty</option>
                      </select>

                      {selectedField.visibilityRule.operator !== 'is_not_empty' && (
                        <input
                          type="text"
                          value={selectedField.visibilityRule.value}
                          onChange={(e) =>
                            updateFieldInActiveForm(selectedField.id, {
                              visibilityRule: { ...selectedField.visibilityRule!, value: e.target.value }
                            })
                          }
                          placeholder="Comparison value"
                          className={`${inputClass} font-mono`}
                        />
                      )}
                    </div>
                  )}
                </div>
              </div>
            </>
          )}
        </aside>
      )}

      {/* 1-Click Generate from SmartObject Modal */}
      {isGenerateModalOpen && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4">
          <form
            onSubmit={handleGenerateFromSO}
            className="bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 rounded-2xl max-w-md w-full p-5 space-y-4 shadow-2xl"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white flex items-center gap-1.5">
                <Sparkles className="w-4 h-4 text-indigo-500" />
                <span>Generate SmartForm from SmartObject</span>
              </h3>
              <button
                type="button"
                onClick={() => setIsGenerateModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3">
              <div>
                <label className="text-xs font-bold block mb-1">Source SmartObject Schema</label>
                <select
                  value={targetSOId}
                  onChange={(e) => setTargetSOId(e.target.value)}
                  className={inputClass}
                >
                  {smartObjects.map((so) => (
                    <option key={so.id} value={so.id}>
                      {so.displayName} ({so.fields.length} fields)
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="text-xs font-bold block mb-1">Auto-Start Workflow on Submit (Optional)</label>
                <select
                  value={targetWfKey}
                  onChange={(e) => setTargetWfKey(e.target.value)}
                  className={inputClass}
                >
                  <option value="">— None —</option>
                  <option value="onboarding">Employee Onboarding Process</option>
                  <option value="expense">Executive Purchase Request</option>
                  <option value="itAccess">IT System Access Request</option>
                </select>
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setIsGenerateModalOpen(false)}
                className="px-3.5 py-2 rounded-xl text-xs font-bold text-slate-500 cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="submit"
                className="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold cursor-pointer"
              >
                Generate Form Now
              </button>
            </div>
          </form>
        </div>
      )}

      {/* New Blank Form Modal */}
      {isNewFormModalOpen && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4">
          <form
            onSubmit={handleCreateBlankForm}
            className="bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 rounded-2xl max-w-md w-full p-5 space-y-4 shadow-2xl"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">Create Blank SmartForm</h3>
              <button
                type="button"
                onClick={() => setIsNewFormModalOpen(false)}
                className="text-slate-400 hover:text-slate-600 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3">
              <div>
                <label className="text-xs font-bold block mb-1">Form Title</label>
                <input
                  type="text"
                  required
                  value={newFormTitle}
                  onChange={(e) => setNewFormTitle(e.target.value)}
                  placeholder="e.g. Vendor Security Questionnaire"
                  className={inputClass}
                />
              </div>
              <div>
                <label className="text-xs font-bold block mb-1">Category</label>
                <select
                  value={newFormCategory}
                  onChange={(e) => setNewFormCategory(e.target.value)}
                  className={inputClass}
                >
                  <option value="General">General</option>
                  <option value="HR">HR</option>
                  <option value="Finance">Finance</option>
                  <option value="IT">IT</option>
                </select>
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <button
                type="button"
                onClick={() => setIsNewFormModalOpen(false)}
                className="px-3.5 py-2 rounded-xl text-xs font-bold text-slate-500 cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="submit"
                className="px-4 py-2 rounded-xl bg-[#2563EB] hover:bg-blue-700 text-white text-xs font-bold cursor-pointer"
              >
                Create Form
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
