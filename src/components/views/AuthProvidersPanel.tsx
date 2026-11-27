import React, { useEffect, useState } from 'react';
import {
  ShieldCheck,
  Plus,
  Pencil,
  Trash2,
  KeyRound,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  X
} from 'lucide-react';
import type { AuthProviderRecord, UpsertAuthProviderPayload } from '../../types/workflow';
import { getWorkflowRepository } from '../../services/repositoryManager';

interface AuthProvidersPanelProps {
  canManage: boolean;
}

export const AuthProvidersPanel: React.FC<AuthProvidersPanelProps> = ({ canManage }) => {
  const [providers, setProviders] = useState<AuthProviderRecord[]>([]);
  const [loading, setLoading] = useState(false);
  const [panelError, setPanelError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  // Modal state
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState<AuthProviderRecord | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Form fields
  const [name, setName] = useState('');
  const [authority, setAuthority] = useState('');
  const [clientId, setClientId] = useState('');
  const [clientSecret, setClientSecret] = useState('');
  const [clearClientSecret, setClearClientSecret] = useState(false);
  const [scopes, setScopes] = useState('openid profile email');
  const [roleMappingJson, setRoleMappingJson] = useState('{}');
  const [defaultRole, setDefaultRole] = useState('User');
  const [jitProvisioningEnabled, setJitProvisioningEnabled] = useState(true);
  const [allowEmailMatching, setAllowEmailMatching] = useState(false);
  const [allowAdminRoleMapping, setAllowAdminRoleMapping] = useState(false);
  const [isEnabled, setIsEnabled] = useState(true);

  const loadProviders = async () => {
    setLoading(true);
    setPanelError(null);
    try {
      const list = await getWorkflowRepository().getAdminAuthProviders();
      setProviders(list);
    } catch (err: any) {
      setPanelError(err?.message || 'Failed to load OIDC providers.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (canManage) {
      void loadProviders();
    }
  }, [canManage]);

  if (!canManage) return null;

  const openCreate = () => {
    setEditing(null);
    setName('');
    setAuthority('https://');
    setClientId('');
    setClientSecret('');
    setClearClientSecret(false);
    setScopes('openid profile email');
    setRoleMappingJson('{}');
    setDefaultRole('User');
    setJitProvisioningEnabled(true);
    setAllowEmailMatching(false);
    setAllowAdminRoleMapping(false);
    setIsEnabled(true);
    setFormError(null);
    setIsModalOpen(true);
  };

  const openEdit = (p: AuthProviderRecord) => {
    setEditing(p);
    setName(p.name);
    setAuthority(p.authority);
    setClientId(p.clientId);
    setClientSecret('');
    setClearClientSecret(false);
    setScopes(p.scopes);
    setRoleMappingJson(p.roleMappingJson || '{}');
    setDefaultRole(p.defaultRole || 'User');
    setJitProvisioningEnabled(p.jitProvisioningEnabled);
    setAllowEmailMatching(p.allowEmailMatching);
    setAllowAdminRoleMapping(p.allowAdminRoleMapping);
    setIsEnabled(p.isEnabled);
    setFormError(null);
    setIsModalOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    try {
      JSON.parse(roleMappingJson || '{}');
    } catch {
      setFormError('RoleMappingJson must be valid JSON (e.g. {"corp-designers": "Designer"}).');
      return;
    }

    const payload: UpsertAuthProviderPayload = {
      name: name.trim(),
      protocol: 'oidc',
      authority: authority.trim(),
      clientId: clientId.trim(),
      clientSecret: clientSecret.trim() ? clientSecret.trim() : undefined,
      clearClientSecret,
      scopes: scopes.trim() || 'openid profile email',
      roleMappingJson: roleMappingJson.trim() || '{}',
      defaultRole: defaultRole.trim() || 'User',
      jitProvisioningEnabled,
      allowEmailMatching,
      allowAdminRoleMapping,
      isEnabled,
      expectedVersion: editing?.version
    };

    setSubmitting(true);
    try {
      const repo = getWorkflowRepository();
      if (editing) {
        await repo.updateAuthProvider(editing.id, payload);
        setFeedback(`OIDC provider "${payload.name}" updated.`);
      } else {
        await repo.createAuthProvider(payload);
        setFeedback(`OIDC provider "${payload.name}" created.`);
      }
      setIsModalOpen(false);
      await loadProviders();
    } catch (err: any) {
      setFormError(err?.message || 'Failed to save OIDC provider.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (p: AuthProviderRecord) => {
    if (!window.confirm(`Delete OIDC provider "${p.name}"? Linked SSO users will no longer be able to sign in with it.`)) {
      return;
    }
    try {
      await getWorkflowRepository().deleteAuthProvider(p.id);
      setFeedback(`OIDC provider "${p.name}" deleted.`);
      await loadProviders();
    } catch (err: any) {
      setPanelError(err?.message || 'Failed to delete OIDC provider.');
    }
  };

  return (
    <div className="p-5 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white flex items-center gap-2">
            <ShieldCheck className="w-4 h-4 text-[#2563EB] dark:text-blue-400" />
            SSO / OpenID Connect Identity Providers
          </h3>
          <p className="text-xs text-[#64748B] dark:text-slate-400">
            Configure enterprise OIDC providers (Authorization Code + PKCE + nonce). Local break-glass login remains available.
          </p>
        </div>

        <button
          type="button"
          onClick={openCreate}
          data-testid="add-oidc-provider-button"
          className="px-3.5 py-2 bg-[#2563EB] hover:bg-blue-700 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 shadow-sm cursor-pointer transition-all"
        >
          <Plus className="w-4 h-4" />
          <span>Add OIDC Provider</span>
        </button>
      </div>

      {panelError && (
        <div role="alert" className="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-xs text-red-600 dark:text-red-300 font-medium">
          {panelError}
        </div>
      )}

      {feedback && (
        <div role="status" className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900 text-xs text-emerald-700 dark:text-emerald-300 font-medium">
          {feedback}
        </div>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-start border-collapse text-xs">
          <thead>
            <tr className="border-b border-[#E2E8F0] dark:border-[#334155] text-[11px] font-bold uppercase tracking-wider text-[#64748B] dark:text-slate-400">
              <th className="pb-2.5 pe-3 text-start">Provider</th>
              <th className="pb-2.5 pe-3 text-start">Authority & Client</th>
              <th className="pb-2.5 pe-3 text-start">Security Policies</th>
              <th className="pb-2.5 pe-3 text-start">Status</th>
              <th className="pb-2.5 text-end">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-[#E2E8F0] dark:divide-[#334155]">
            {loading ? (
              <tr>
                <td colSpan={5} className="py-6 text-center text-slate-400">Loading OIDC providers...</td>
              </tr>
            ) : providers.length === 0 ? (
              <tr>
                <td colSpan={5} className="py-6 text-center text-slate-400">
                  No external OIDC providers configured yet.
                </td>
              </tr>
            ) : (
              providers.map((p) => (
                <tr key={p.id} className="hover:bg-[#F8FAFC] dark:hover:bg-slate-800/60 transition-colors">
                  <td className="py-3 pe-3">
                    <div className="font-extrabold text-[#0F172A] dark:text-white">{p.name}</div>
                    <div className="text-[11px] text-slate-400 font-mono uppercase">{p.protocol}</div>
                  </td>
                  <td className="py-3 pe-3">
                    <div className="font-mono text-[11px] text-[#0F172A] dark:text-slate-200">{p.authority}</div>
                    <div className="text-[11px] text-slate-500 flex items-center gap-2 mt-0.5">
                      <span>Client: <code className="font-mono">{p.clientId}</code></span>
                      {p.hasSecret && (
                        <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-300 text-[10px] font-bold">
                          <KeyRound className="w-3 h-3" /> Secret encrypted
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="py-3 pe-3">
                    <div className="flex flex-wrap gap-1.5 text-[10px] font-bold">
                      <span className={`px-2 py-0.5 rounded-full border ${p.jitProvisioningEnabled ? 'bg-blue-50 dark:bg-blue-950/50 text-blue-700 dark:text-blue-300 border-blue-200 dark:border-blue-800' : 'bg-slate-100 dark:bg-slate-800 text-slate-500 border-slate-200 dark:border-slate-700'}`}>
                        JIT: {p.jitProvisioningEnabled ? 'On' : 'Off'}
                      </span>
                      <span className={`px-2 py-0.5 rounded-full border ${p.allowEmailMatching ? 'bg-amber-50 dark:bg-amber-950/50 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-800' : 'bg-slate-100 dark:bg-slate-800 text-slate-500 border-slate-200 dark:border-slate-700'}`}>
                        Email Match: {p.allowEmailMatching ? 'Allowed' : 'Verified Only'}
                      </span>
                      {p.allowAdminRoleMapping && (
                        <span className="px-2 py-0.5 rounded-full border bg-red-50 dark:bg-red-950/50 text-red-700 dark:text-red-300 border-red-200 dark:border-red-800">
                          Admin Mapping
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="py-3 pe-3">
                    {p.isEnabled ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-50 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
                        <CheckCircle2 className="w-3 h-3" /> Enabled
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-500 border border-slate-200 dark:border-slate-700">
                        <XCircle className="w-3 h-3" /> Disabled
                      </span>
                    )}
                  </td>
                  <td className="py-3 text-end">
                    <div className="inline-flex items-center gap-1">
                      <button
                        type="button"
                        onClick={() => openEdit(p)}
                        aria-label={`Edit OIDC provider ${p.name}`}
                        className="p-1.5 text-slate-400 hover:text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-950/40 rounded-lg cursor-pointer"
                      >
                        <Pencil className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(p)}
                        aria-label={`Delete OIDC provider ${p.name}`}
                        className="p-1.5 text-slate-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-950/40 rounded-lg cursor-pointer"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Add / Edit OIDC Provider Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs">
          <form
            onSubmit={handleSubmit}
            role="dialog"
            aria-label={editing ? 'Edit OIDC Provider' : 'Add OIDC Provider'}
            className="w-full max-w-lg max-h-[90vh] overflow-y-auto bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] rounded-3xl shadow-2xl p-6 space-y-4 text-xs"
          >
            <div className="flex items-center justify-between pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
              <h4 className="font-extrabold text-sm text-[#0F172A] dark:text-white flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-[#2563EB]" />
                {editing ? `Edit OIDC Provider: ${editing.name}` : 'Add OIDC Provider'}
              </h4>
              <button
                type="button"
                onClick={() => setIsModalOpen(false)}
                aria-label="Close OIDC provider modal"
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1 cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {formError && (
              <div role="alert" className="p-3 rounded-xl bg-red-50 dark:bg-red-950/40 border border-red-200 dark:border-red-900 text-red-600 dark:text-red-300 font-medium">
                {formError}
              </div>
            )}

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label htmlFor="oidc-name" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                  Provider Name
                </label>
                <input
                  id="oidc-name"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="e.g. Microsoft Entra ID"
                  className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100"
                />
              </div>
              <div>
                <label htmlFor="oidc-client-id" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                  Client ID
                </label>
                <input
                  id="oidc-client-id"
                  required
                  value={clientId}
                  onChange={(e) => setClientId(e.target.value)}
                  placeholder="tamam-studio-client"
                  className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100"
                />
              </div>
            </div>

            <div>
              <label htmlFor="oidc-authority" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                Authority (HTTPS)
              </label>
              <input
                id="oidc-authority"
                type="url"
                required
                value={authority}
                onChange={(e) => setAuthority(e.target.value)}
                placeholder="https://login.microsoftonline.com/<tenant>/v2.0"
                className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl font-mono text-[#0F172A] dark:text-slate-100"
              />
            </div>

            <div>
              <label htmlFor="oidc-client-secret" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                Client Secret {editing?.hasSecret ? '(leave blank to keep encrypted secret)' : '(optional for public PKCE)'}
              </label>
              <input
                id="oidc-client-secret"
                type="password"
                autoComplete="new-password"
                value={clientSecret}
                onChange={(e) => setClientSecret(e.target.value)}
                placeholder={editing?.hasSecret ? '•••••••• (encrypted)' : 'Enter client secret'}
                className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100"
              />
              {editing?.hasSecret && (
                <label htmlFor="oidc-clear-secret" className="mt-1.5 inline-flex items-center gap-2 text-[11px] text-slate-500 cursor-pointer">
                  <input
                    id="oidc-clear-secret"
                    type="checkbox"
                    checked={clearClientSecret}
                    onChange={(e) => setClearClientSecret(e.target.checked)}
                  />
                  Remove stored client secret
                </label>
              )}
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label htmlFor="oidc-scopes" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                  Scopes
                </label>
                <input
                  id="oidc-scopes"
                  value={scopes}
                  onChange={(e) => setScopes(e.target.value)}
                  className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl font-mono text-[#0F172A] dark:text-slate-100"
                />
              </div>
              <div>
                <label htmlFor="oidc-default-role" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                  Default Role (non-Admin)
                </label>
                <input
                  id="oidc-default-role"
                  value={defaultRole}
                  onChange={(e) => setDefaultRole(e.target.value)}
                  className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl text-[#0F172A] dark:text-slate-100"
                />
              </div>
            </div>

            <div>
              <label htmlFor="oidc-role-mapping" className="font-bold block mb-1 text-[#0F172A] dark:text-slate-200">
                RoleMappingJson
              </label>
              <textarea
                id="oidc-role-mapping"
                rows={3}
                value={roleMappingJson}
                onChange={(e) => setRoleMappingJson(e.target.value)}
                placeholder='{"grp-designers": "Designer"}'
                className="w-full px-3 py-2 bg-[#F8FAFC] dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl font-mono text-[#0F172A] dark:text-slate-100"
              />
            </div>

            <div className="space-y-2 pt-1">
              <label htmlFor="oidc-enabled" className="flex items-center gap-2 cursor-pointer">
                <input
                  id="oidc-enabled"
                  type="checkbox"
                  checked={isEnabled}
                  onChange={(e) => setIsEnabled(e.target.checked)}
                />
                <span className="font-semibold text-[#0F172A] dark:text-slate-200">Enabled (show on login screen)</span>
              </label>

              <label htmlFor="oidc-jit" className="flex items-center gap-2 cursor-pointer">
                <input
                  id="oidc-jit"
                  type="checkbox"
                  checked={jitProvisioningEnabled}
                  onChange={(e) => setJitProvisioningEnabled(e.target.checked)}
                />
                <span className="font-semibold text-[#0F172A] dark:text-slate-200">
                  JitProvisioningEnabled (auto-create new users on first SSO login)
                </span>
              </label>

              <label htmlFor="oidc-email-matching" className="flex items-center gap-2 cursor-pointer">
                <input
                  id="oidc-email-matching"
                  type="checkbox"
                  checked={allowEmailMatching}
                  onChange={(e) => setAllowEmailMatching(e.target.checked)}
                />
                <span className="font-semibold text-[#0F172A] dark:text-slate-200">
                  AllowEmailMatching (link unverified emails to existing local accounts — not recommended)
                </span>
              </label>

              <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800">
                <label htmlFor="oidc-allow-admin-mapping" className="flex items-start gap-2 cursor-pointer">
                  <input
                    id="oidc-allow-admin-mapping"
                    type="checkbox"
                    checked={allowAdminRoleMapping}
                    onChange={(e) => setAllowAdminRoleMapping(e.target.checked)}
                    className="mt-0.5"
                  />
                  <span className="text-amber-800 dark:text-amber-200">
                    <strong className="inline-flex items-center gap-1">
                      <AlertTriangle className="w-3.5 h-3.5" />
                      AllowAdminRoleMapping (explicit confirmation)
                    </strong>
                    <span className="block text-[11px] opacity-90">
                      Required before mapping any external IdP group to an Admin / Administrator role.
                    </span>
                  </span>
                </label>
              </div>
            </div>

            <div className="flex justify-end gap-2 pt-3 border-t border-[#E2E8F0] dark:border-[#334155]">
              <button
                type="button"
                onClick={() => setIsModalOpen(false)}
                className="px-3.5 py-1.5 font-bold text-slate-600 dark:text-slate-300 cursor-pointer"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="px-4 py-1.5 bg-[#2563EB] hover:bg-blue-700 disabled:opacity-50 text-white font-bold rounded-xl cursor-pointer"
              >
                {submitting ? 'Saving...' : (editing ? 'Save Provider' : 'Create Provider')}
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
