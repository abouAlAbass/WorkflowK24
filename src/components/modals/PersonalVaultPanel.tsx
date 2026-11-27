import React, { useEffect, useState, useCallback } from 'react';
import { KeyRound, ShieldCheck, Trash2, Save, CheckCircle2, AlertCircle, Lock } from 'lucide-react';
import { getWorkflowRepository } from '../../services/repositoryManager';
import type { UserConnectionCredentialRecord } from '../../types/workflow';

interface PersonalVaultPanelProps {
  highlightConnectionId?: string | null;
  onCredentialSaved?: (connectionId: string) => void;
}

export const PersonalVaultPanel: React.FC<PersonalVaultPanelProps> = ({
  highlightConnectionId,
  onCredentialSaved
}) => {
  const [entries, setEntries] = useState<UserConnectionCredentialRecord[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [selectedConnectionId, setSelectedConnectionId] = useState<string>(highlightConnectionId || '');
  const [credentialMode, setCredentialMode] = useState<'token' | 'basic' | 'json'>('token');
  const [tokenValue, setTokenValue] = useState('');
  const [usernameValue, setUsernameValue] = useState('');
  const [passwordValue, setPasswordValue] = useState('');
  const [rawJsonValue, setRawJsonValue] = useState('');
  const [saving, setSaving] = useState(false);

  const loadCredentials = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await getWorkflowRepository().getMyConnectionCredentials();
      setEntries(list);
      if (highlightConnectionId) {
        setSelectedConnectionId(highlightConnectionId);
      } else if (list.length > 0 && !selectedConnectionId) {
        setSelectedConnectionId(list[0].connectionId);
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Unable to load personal credentials.');
    } finally {
      setLoading(false);
    }
  }, [highlightConnectionId, selectedConnectionId]);

  useEffect(() => {
    loadCredentials();
  }, [loadCredentials]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedConnectionId) {
      setError('Please select a connection.');
      return;
    }

    let secretJson = '';
    if (credentialMode === 'token') {
      if (!tokenValue.trim()) {
        setError('Access token / API key is required.');
        return;
      }
      secretJson = JSON.stringify({ token: tokenValue.trim() });
    } else if (credentialMode === 'basic') {
      if (!usernameValue.trim() || !passwordValue) {
        setError('Username and password are required.');
        return;
      }
      secretJson = JSON.stringify({ username: usernameValue.trim(), password: passwordValue });
    } else {
      if (!rawJsonValue.trim()) {
        setError('JSON credential payload is required.');
        return;
      }
      try {
        JSON.parse(rawJsonValue);
      } catch {
        setError('Credential payload must be valid JSON.');
        return;
      }
      secretJson = rawJsonValue.trim();
    }

    setSaving(true);
    setError(null);
    setSuccessMsg(null);
    try {
      await getWorkflowRepository().upsertMyConnectionCredential(selectedConnectionId, secretJson);
      setTokenValue('');
      setUsernameValue('');
      setPasswordValue('');
      setRawJsonValue('');
      setSuccessMsg('Identifiants enregistrés de manière chiffrée dans votre coffre personnel.');
      await loadCredentials();
      onCredentialSaved?.(selectedConnectionId);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save personal credential.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (connectionId: string) => {
    setError(null);
    setSuccessMsg(null);
    try {
      await getWorkflowRepository().deleteMyConnectionCredential(connectionId);
      setSuccessMsg('Identifiants révoqués et supprimés.');
      await loadCredentials();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete personal credential.');
    }
  };

  return (
    <div className="p-6 rounded-3xl bg-white dark:bg-[#1E293B] border border-[#E2E8F0] dark:border-[#334155] shadow-xs space-y-5 text-start">
      <div className="flex items-center justify-between pb-3 border-b border-[#E2E8F0] dark:border-[#334155]">
        <div className="flex items-center gap-2">
          <Lock className="w-5 h-5 text-indigo-600 dark:text-indigo-400" />
          <div>
            <h3 className="text-sm font-extrabold text-[#0F172A] dark:text-white">
              Mes Identifiants Externes (Coffre Personnel)
            </h3>
            <p className="text-[11px] text-[#64748B] dark:text-slate-400">
              Vos identifiants personnels pour les connexions en mode <code className="font-mono">user_sso_vault</code> sont chiffrés et ne sont jamais réaffichés.
            </p>
          </div>
        </div>
        <span className="px-2.5 py-1 rounded-full bg-indigo-50 dark:bg-indigo-950/50 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800 text-[11px] font-bold flex items-center gap-1">
          <ShieldCheck className="w-3.5 h-3.5" /> Chiffrement isolé
        </span>
      </div>

      {error && (
        <div role="alert" className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-xs font-semibold text-rose-700 dark:text-rose-300 flex items-center gap-2">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {successMsg && (
        <div role="status" className="p-3 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-xs font-semibold text-emerald-700 dark:text-emerald-300 flex items-center gap-2">
          <CheckCircle2 className="w-4 h-4 shrink-0" />
          <span>{successMsg}</span>
        </div>
      )}

      {loading ? (
        <p className="text-xs text-[#64748B] dark:text-slate-400">Chargement de votre coffre personnel...</p>
      ) : entries.length === 0 ? (
        <p className="text-xs text-[#64748B] dark:text-slate-400">
          Aucune connexion en mode <code className="font-mono">user_sso_vault</code> n&apos;est configurée pour le moment.
        </p>
      ) : (
        <div className="space-y-4">
          <div className="overflow-x-auto">
            <table className="w-full text-xs text-start border-collapse">
              <thead>
                <tr className="border-b border-slate-200 dark:border-slate-700 text-[#64748B] dark:text-slate-400 uppercase text-[10px]">
                  <th className="py-2 pe-3 text-start">Connexion</th>
                  <th className="py-2 pe-3 text-start">Service</th>
                  <th className="py-2 pe-3 text-start">Statut</th>
                  <th className="py-2 pe-3 text-start">Dernière mise à jour</th>
                  <th className="py-2 text-end">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {entries.map((entry) => (
                  <tr
                    key={entry.connectionId}
                    className={
                      entry.connectionId === selectedConnectionId
                        ? 'bg-indigo-50/50 dark:bg-indigo-950/30'
                        : 'hover:bg-slate-50 dark:hover:bg-slate-800/50'
                    }
                  >
                    <td className="py-2.5 pe-3 font-mono font-bold text-[#0F172A] dark:text-slate-100">
                      {entry.connectionName}
                    </td>
                    <td className="py-2.5 pe-3">
                      <span className="px-2 py-0.5 rounded bg-slate-100 dark:bg-slate-800 font-mono text-[11px]">
                        {entry.serviceType}
                      </span>
                    </td>
                    <td className="py-2.5 pe-3">
                      {entry.hasCredentials ? (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300 font-semibold text-[11px]">
                          <KeyRound className="w-3 h-3" /> Configuré
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-amber-50 dark:bg-amber-950/50 text-amber-700 dark:text-amber-300 font-semibold text-[11px]">
                          Non renseigné
                        </span>
                      )}
                    </td>
                    <td className="py-2.5 pe-3 text-[11px] text-[#64748B] dark:text-slate-400">
                      {entry.updatedAt ? new Date(entry.updatedAt).toLocaleString() : '-'}
                    </td>
                    <td className="py-2.5 text-end">
                      <div className="inline-flex items-center gap-1.5">
                        <button
                          type="button"
                          onClick={() => setSelectedConnectionId(entry.connectionId)}
                          className="px-2.5 py-1 rounded-lg bg-indigo-50 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300 font-bold text-[11px] hover:bg-indigo-100 cursor-pointer"
                        >
                          {entry.hasCredentials ? 'Mettre à jour' : 'Renseigner'}
                        </button>
                        {entry.hasCredentials && (
                          <button
                            type="button"
                            onClick={() => handleDelete(entry.connectionId)}
                            aria-label={`Révoquer identifiants ${entry.connectionName}`}
                            className="p-1.5 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40 cursor-pointer"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <form onSubmit={handleSave} className="p-4 rounded-2xl bg-[#F8FAFC] dark:bg-slate-900/60 border border-[#E2E8F0] dark:border-slate-800 space-y-3 text-xs">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label htmlFor="vault-conn-select" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                  Connexion cible
                </label>
                <select
                  id="vault-conn-select"
                  value={selectedConnectionId}
                  onChange={(e) => setSelectedConnectionId(e.target.value)}
                  className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
                >
                  {entries.map((e) => (
                    <option key={e.connectionId} value={e.connectionId}>
                      {e.connectionName} ({e.serviceType})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label htmlFor="vault-mode-select" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                  Format d&apos;identifiants
                </label>
                <select
                  id="vault-mode-select"
                  value={credentialMode}
                  onChange={(e) => setCredentialMode(e.target.value as 'token' | 'basic' | 'json')}
                  className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-bold text-[#0F172A] dark:text-slate-100"
                >
                  <option value="token">Token / API Key</option>
                  <option value="basic">Utilisateur &amp; Mot de passe</option>
                  <option value="json">JSON personnalisé (ex. PostgreSQL)</option>
                </select>
              </div>
            </div>

            {credentialMode === 'token' && (
              <div>
                <label htmlFor="vault-token-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                  Token / Clé secrète
                </label>
                <input
                  id="vault-token-input"
                  type="password"
                  value={tokenValue}
                  onChange={(e) => setTokenValue(e.target.value)}
                  placeholder="Entrez votre jeton personnel..."
                  className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
                />
              </div>
            )}

            {credentialMode === 'basic' && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                <div>
                  <label htmlFor="vault-user-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                    Nom d&apos;utilisateur
                  </label>
                  <input
                    id="vault-user-input"
                    type="text"
                    value={usernameValue}
                    onChange={(e) => setUsernameValue(e.target.value)}
                    placeholder="user@corp.internal"
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
                  />
                </div>
                <div>
                  <label htmlFor="vault-pass-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                    Mot de passe
                  </label>
                  <input
                    id="vault-pass-input"
                    type="password"
                    value={passwordValue}
                    onChange={(e) => setPasswordValue(e.target.value)}
                    placeholder="••••••••"
                    className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
                  />
                </div>
              </div>
            )}

            {credentialMode === 'json' && (
              <div>
                <label htmlFor="vault-json-input" className="font-bold text-[#0F172A] dark:text-slate-200 block mb-1">
                  Payload JSON (chiffré au repos)
                </label>
                <textarea
                  id="vault-json-input"
                  rows={3}
                  value={rawJsonValue}
                  onChange={(e) => setRawJsonValue(e.target.value)}
                  placeholder='{"username":"app_user","password":"..."}'
                  className="w-full bg-white dark:bg-slate-900 border border-[#E2E8F0] dark:border-slate-700 rounded-xl p-2 text-xs font-mono text-[#0F172A] dark:text-slate-100"
                />
              </div>
            )}

            <div className="flex justify-end">
              <button
                type="submit"
                disabled={saving}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-xl text-xs font-bold flex items-center gap-1.5 cursor-pointer"
              >
                <Save className="w-3.5 h-3.5" />
                <span>{saving ? 'Enregistrement...' : 'Enregistrer dans mon coffre'}</span>
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
