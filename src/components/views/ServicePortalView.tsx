import React, { useState, useEffect, useMemo } from 'react';
import {
  Search,
  FileSpreadsheet,
  Clock,
  CheckCircle2,
  Send,
  Play,
  Eye,
  X,
  FolderOpen,
  FileEdit,
  RefreshCw,
  AlertCircle
} from 'lucide-react';
import { useSmartFormStore } from '../../store/useSmartFormStore';
import { SmartFormRenderer } from '../forms/SmartFormRenderer';
import { InstanceViewFlowPanel } from './InstanceViewFlowPanel';
import type { SmartFormDefinition, SmartFormSubmission } from '../../types/smartform';

type PortalTab = 'catalog' | 'my-requests';

export const ServicePortalView: React.FC = () => {
  const {
    forms,
    mySubmissions,
    syncForms,
    fetchMySubmissions,
    saveDraft,
    submitForm
  } = useSmartFormStore();

  const [activeTab, setActiveTab] = useState<PortalTab>('catalog');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [activeForm, setActiveForm] = useState<SmartFormDefinition | null>(null);
  const [activeDraft, setActiveDraft] = useState<SmartFormSubmission | null>(null);
  const [viewFlowInstanceId, setViewFlowInstanceId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);

  useEffect(() => {
    void syncForms();
    void fetchMySubmissions();
  }, [syncForms, fetchMySubmissions]);

  const publishedForms = useMemo(
    () => forms.filter((f) => f.status === 'Published'),
    [forms]
  );

  const categories = useMemo(() => {
    const set = new Set<string>(['All']);
    publishedForms.forEach((f) => {
      if (f.category) set.add(f.category);
    });
    return Array.from(set);
  }, [publishedForms]);

  const filteredCatalog = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return publishedForms.filter((f) => {
      const catOk = selectedCategory === 'All' || f.category === selectedCategory;
      const qOk =
        !q ||
        f.title.toLowerCase().includes(q) ||
        f.description.toLowerCase().includes(q) ||
        f.category.toLowerCase().includes(q);
      return catOk && qOk;
    });
  }, [publishedForms, searchQuery, selectedCategory]);

  const drafts = useMemo(
    () => mySubmissions.filter((s) => s.status === 'Draft'),
    [mySubmissions]
  );

  const submittedRequests = useMemo(
    () => mySubmissions.filter((s) => s.status !== 'Draft'),
    [mySubmissions]
  );

  const handleOpenNewRequest = (form: SmartFormDefinition) => {
    setFeedback(null);
    setActiveDraft(null);
    setActiveForm(form);
  };

  const handleResumeDraft = (draft: SmartFormSubmission) => {
    const form = forms.find((f) => f.id === draft.smartFormId);
    if (!form) {
      setFeedback({ type: 'error', text: `Form '${draft.smartFormId}' is no longer available.` });
      return;
    }
    setFeedback(null);
    setActiveDraft(draft);
    setActiveForm(form);
  };

  const handleSaveDraft = async (values: Record<string, unknown>) => {
    if (!activeForm) return;
    const saved = await saveDraft(activeForm.id, values, activeDraft?.id);
    setActiveDraft(saved);
    setFeedback({
      type: 'success',
      text: `Draft saved for "${activeForm.title}". You can resume it anytime from My Requests & Drafts.`
    });
  };

  const handleSubmitRequest = async (
    values: Record<string, unknown>,
    meta?: { idempotencyKey: string }
  ) => {
    if (!activeForm) return;
    const submitted = await submitForm(
      activeForm.id,
      values,
      meta?.idempotencyKey,
      activeDraft?.id
    );
    setActiveForm(null);
    setActiveDraft(null);
    setActiveTab('my-requests');
    setFeedback({
      type: 'success',
      text: submitted.linkedInstanceId
        ? `Request submitted! Workflow instance ${submitted.linkedInstanceId} has been started.`
        : `Request "${activeForm.title}" submitted successfully.`
    });
    void fetchMySubmissions();
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    try {
      await Promise.all([syncForms(), fetchMySubmissions()]);
    } finally {
      setIsRefreshing(false);
    }
  };

  const renderStatusBadge = (sub: SmartFormSubmission) => {
    if (sub.status === 'Draft') {
      return (
        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-amber-50 dark:bg-amber-950/50 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
          <FileEdit className="w-3 h-3" />
          Draft
        </span>
      );
    }

    const status = (sub.instanceStatus || 'SUBMITTED').toUpperCase();
    if (status === 'COMPLETED') {
      return (
        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
          <CheckCircle2 className="w-3 h-3" />
          Completed
        </span>
      );
    }
    if (status === 'FAILED' || status === 'FAULTED' || status === 'CANCELLED') {
      return (
        <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-rose-50 dark:bg-rose-950/50 text-rose-700 dark:text-rose-400 border border-rose-200 dark:border-rose-800">
          <AlertCircle className="w-3 h-3" />
          {status}
        </span>
      );
    }
    return (
      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-blue-50 dark:bg-blue-950/50 text-[#2563EB] dark:text-blue-400 border border-blue-200 dark:border-blue-800">
        <Clock className="w-3 h-3" />
        {status}
      </span>
    );
  };

  return (
    <div className="flex-1 overflow-y-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-extrabold text-[#0F172A] dark:text-white tracking-tight">
            Service Portal
          </h2>
          <p className="text-xs text-[#64748B] dark:text-slate-400 mt-1">
            Start business requests from published forms, save drafts, and track your active submissions in real time.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <div className="inline-flex p-1 rounded-xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 shadow-2xs">
            <button
              type="button"
              onClick={() => setActiveTab('catalog')}
              className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-colors cursor-pointer flex items-center gap-1.5 ${
                activeTab === 'catalog'
                  ? 'bg-[#2563EB] text-white shadow-xs'
                  : 'text-[#475569] dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800'
              }`}
            >
              <FolderOpen className="w-3.5 h-3.5" />
              <span>Service Catalog</span>
              <span className="text-[10px] font-mono px-1.5 py-0.2 rounded-full bg-black/10 dark:bg-white/10">
                {publishedForms.length}
              </span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('my-requests')}
              className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-colors cursor-pointer flex items-center gap-1.5 ${
                activeTab === 'my-requests'
                  ? 'bg-[#2563EB] text-white shadow-xs'
                  : 'text-[#475569] dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800'
              }`}
            >
              <Send className="w-3.5 h-3.5" />
              <span>My Requests &amp; Drafts</span>
              <span className="text-[10px] font-mono px-1.5 py-0.2 rounded-full bg-black/10 dark:bg-white/10">
                {mySubmissions.length}
              </span>
            </button>
          </div>

          <button
            type="button"
            onClick={() => void handleRefresh()}
            disabled={isRefreshing}
            aria-label="Refresh portal data"
            className="p-2 rounded-xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 text-[#475569] dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
          >
            <RefreshCw className={`w-4 h-4 ${isRefreshing ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {/* Feedback Banner */}
      {feedback && (
        <div
          role="alert"
          className={`p-3.5 rounded-2xl border text-xs font-semibold flex items-center justify-between gap-2 ${
            feedback.type === 'success'
              ? 'bg-emerald-50/80 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-300'
              : 'bg-rose-50/80 dark:bg-rose-950/30 border-rose-200 dark:border-rose-800 text-rose-800 dark:text-rose-300'
          }`}
        >
          <div className="flex items-center gap-2">
            {feedback.type === 'success' ? (
              <CheckCircle2 className="w-4 h-4 shrink-0" />
            ) : (
              <AlertCircle className="w-4 h-4 shrink-0" />
            )}
            <span>{feedback.text}</span>
          </div>
          <button
            type="button"
            onClick={() => setFeedback(null)}
            aria-label="Dismiss message"
            className="p-1 opacity-70 hover:opacity-100 cursor-pointer"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Read-Only ViewFlow Inspector */}
      {viewFlowInstanceId && (
        <InstanceViewFlowPanel
          instanceId={viewFlowInstanceId}
          readOnly={true}
          onClose={() => setViewFlowInstanceId(null)}
        />
      )}

      {/* Tab 1: Service Catalog */}
      {activeTab === 'catalog' && (
        <div className="space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="relative flex-1 max-w-md">
              <Search className="w-4 h-4 text-slate-400 absolute start-3 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Search service catalog..."
                aria-label="Search service catalog"
                className="w-full ps-9 pe-3.5 py-2 rounded-xl text-xs bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 text-[#0F172A] dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-[#2563EB]"
              />
            </div>

            <div className="flex items-center gap-1.5 flex-wrap">
              {categories.map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => setSelectedCategory(cat)}
                  className={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-colors cursor-pointer ${
                    selectedCategory === cat
                      ? 'bg-[#2563EB] text-white'
                      : 'bg-white dark:bg-[#111827] text-[#475569] dark:text-slate-300 border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800'
                  }`}
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>

          {filteredCatalog.length === 0 ? (
            <div className="p-10 rounded-3xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 text-center space-y-2">
              <FileSpreadsheet className="w-8 h-8 text-slate-400 mx-auto" />
              <p className="text-sm font-bold text-[#0F172A] dark:text-white">No published services found</p>
              <p className="text-xs text-[#64748B] dark:text-slate-400">
                Try adjusting your search query or category filter.
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filteredCatalog.map((form) => (
                <article
                  key={form.id}
                  className="p-5 rounded-3xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 shadow-2xs hover:shadow-md transition-all flex flex-col justify-between gap-4"
                >
                  <div className="space-y-2">
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-[10px] font-bold uppercase tracking-wider px-2.5 py-0.5 rounded-full bg-blue-50 dark:bg-blue-950/50 text-[#2563EB] dark:text-blue-400 border border-blue-200/70 dark:border-blue-800">
                        {form.category || 'General'}
                      </span>
                      <span className="text-[10px] font-mono text-[#64748B] dark:text-slate-400">
                        {form.fields.filter((f) => f.type !== 'section').length} fields
                      </span>
                    </div>
                    <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">
                      {form.title}
                    </h3>
                    <p className="text-xs text-[#64748B] dark:text-slate-400 line-clamp-3">
                      {form.description || 'Submit a new request using this published form.'}
                    </p>
                  </div>

                  <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
                    <span className="text-[11px] text-[#64748B] dark:text-slate-400">
                      {form.linkedWorkflowKey ? 'Automated workflow' : 'Direct submission'}
                    </span>
                    <button
                      type="button"
                      onClick={() => handleOpenNewRequest(form)}
                      className="px-3.5 py-1.5 rounded-xl bg-[#2563EB] hover:bg-blue-700 text-white text-xs font-bold inline-flex items-center gap-1.5 cursor-pointer transition-colors"
                    >
                      <Play className="w-3.5 h-3.5" />
                      <span>Start Request</span>
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Tab 2: My Requests & Drafts */}
      {activeTab === 'my-requests' && (
        <div className="space-y-6">
          {/* Saved Drafts */}
          <section className="space-y-3">
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
              <FileEdit className="w-4 h-4 text-amber-500" />
              <span>Saved Drafts ({drafts.length})</span>
            </h3>
            {drafts.length === 0 ? (
              <div className="p-6 rounded-2xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 text-xs text-[#64748B] dark:text-slate-400">
                No saved drafts. Start any request from the Service Catalog and click "Save Draft" to continue later.
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                {drafts.map((draft) => (
                  <div
                    key={draft.id}
                    className="p-4 rounded-2xl bg-white dark:bg-[#111827] border border-amber-200/80 dark:border-amber-900/60 flex items-center justify-between gap-3"
                  >
                    <div className="min-w-0 space-y-1">
                      <div className="flex items-center gap-2">
                        <p className="text-xs font-extrabold text-[#0F172A] dark:text-white truncate">
                          {draft.smartFormTitle || draft.smartFormId}
                        </p>
                        {renderStatusBadge(draft)}
                      </div>
                      <p className="text-[11px] text-[#64748B] dark:text-slate-400 font-mono">
                        Last saved: {new Date(draft.updatedAt || draft.submittedAt).toLocaleString()}
                      </p>
                    </div>
                    <button
                      type="button"
                      onClick={() => handleResumeDraft(draft)}
                      className="px-3.5 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-600 text-white text-xs font-bold shrink-0 cursor-pointer transition-colors"
                    >
                      Resume Draft
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Submitted Requests */}
          <section className="space-y-3">
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
              <Send className="w-4 h-4 text-[#2563EB]" />
              <span>Submitted Requests ({submittedRequests.length})</span>
            </h3>
            {submittedRequests.length === 0 ? (
              <div className="p-6 rounded-2xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 text-xs text-[#64748B] dark:text-slate-400">
                You have not submitted any requests yet.
              </div>
            ) : (
              <div className="rounded-2xl bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 overflow-hidden">
                <div className="overflow-x-auto">
                  <table className="w-full text-xs">
                    <thead className="bg-slate-50 dark:bg-slate-900/80 text-[#475569] dark:text-slate-300 border-b border-slate-200 dark:border-slate-800">
                      <tr>
                        <th className="text-start px-4 py-3 font-bold">Request</th>
                        <th className="text-start px-4 py-3 font-bold">Submitted</th>
                        <th className="text-start px-4 py-3 font-bold">Status</th>
                        <th className="text-start px-4 py-3 font-bold">Workflow Instance</th>
                        <th className="text-end px-4 py-3 font-bold">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                      {submittedRequests.map((sub) => (
                        <tr key={sub.id} className="hover:bg-slate-50/70 dark:hover:bg-slate-800/40">
                          <td className="px-4 py-3">
                            <p className="font-bold text-[#0F172A] dark:text-white">
                              {sub.smartFormTitle || sub.smartFormId}
                            </p>
                            <p className="text-[10px] font-mono text-[#64748B] dark:text-slate-400">{sub.id}</p>
                          </td>
                          <td className="px-4 py-3 text-[#475569] dark:text-slate-300">
                            {new Date(sub.submittedAt).toLocaleString()}
                          </td>
                          <td className="px-4 py-3">{renderStatusBadge(sub)}</td>
                          <td className="px-4 py-3 font-mono text-[11px] text-[#475569] dark:text-slate-300">
                            {sub.linkedInstanceId || '—'}
                          </td>
                          <td className="px-4 py-3 text-end">
                            {sub.linkedInstanceId && (
                              <button
                                type="button"
                                onClick={() => setViewFlowInstanceId(sub.linkedInstanceId!)}
                                className="px-3 py-1.5 rounded-lg border border-blue-200 dark:border-blue-800 bg-blue-50/70 dark:bg-blue-950/40 text-[#2563EB] dark:text-blue-400 text-[11px] font-bold inline-flex items-center gap-1 cursor-pointer hover:bg-blue-100 dark:hover:bg-blue-950"
                              >
                                <Eye className="w-3.5 h-3.5" />
                                <span>ViewFlow</span>
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </section>
        </div>
      )}

      {/* Request Submission / Draft Resume Modal */}
      {activeForm && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={activeForm.title}
          className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4"
        >
          <div className="bg-white dark:bg-[#111827] border border-slate-200 dark:border-slate-800 rounded-3xl max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden shadow-2xl">
            <div className="px-6 py-4 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-extrabold text-[#0F172A] dark:text-white">
                    {activeForm.title}
                  </h3>
                  {activeDraft && (
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-100 dark:bg-amber-950 text-amber-700 dark:text-amber-400">
                      Resuming Draft
                    </span>
                  )}
                </div>
                {activeForm.description && (
                  <p className="text-xs text-[#64748B] dark:text-slate-400 mt-0.5">{activeForm.description}</p>
                )}
              </div>
              <button
                type="button"
                onClick={() => {
                  setActiveForm(null);
                  setActiveDraft(null);
                }}
                aria-label="Close request form"
                className="p-1.5 rounded-xl text-slate-400 hover:text-slate-700 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-6">
              <SmartFormRenderer
                key={`${activeForm.id}-${activeDraft?.id || 'new'}`}
                form={activeForm}
                initialValues={activeDraft?.data}
                submitLabel="Submit Request"
                draftLabel="Save Draft"
                onSaveDraft={handleSaveDraft}
                onSubmit={handleSubmitRequest}
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
