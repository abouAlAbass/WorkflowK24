import React, { useEffect, useState } from 'react';
import { Play, Server, Lock, CheckCircle2, AlertCircle, Sparkles, LogIn, ShieldCheck } from 'lucide-react';
import { repositoryManager } from '../../services/repositoryManager';
import type { AppMode } from '../../services/workflowRepository';
import type { PublicAuthProvider } from '../../types/workflow';
import { useWorkflowStore } from '../../store/useWorkflowStore';

interface ModeSelectorModalProps {
  isOpen: boolean;
  onClose: () => void;
  canDismiss?: boolean;
}

export const ModeSelectorModal: React.FC<ModeSelectorModalProps> = ({
  isOpen,
  onClose,
  canDismiss = true
}) => {
  const [selectedMode, setSelectedMode] = useState<AppMode>(repositoryManager.getMode());
  const [email, setEmail] = useState('adel.sebti@corp.internal');
  const [password, setPassword] = useState('Password123!');
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [ssoProviders, setSsoProviders] = useState<PublicAuthProvider[]>([]);

  const syncData = useWorkflowStore((state) => state.syncDataFromRepository);

  useEffect(() => {
    if (!isOpen || selectedMode !== 'connected') return;
    let cancelled = false;
    repositoryManager
      .getHttpRepository()
      .getPublicAuthProviders()
      .then((res) => {
        if (!cancelled) {
          setSsoProviders(res.providers || []);
        }
      })
      .catch(() => {
        if (!cancelled) setSsoProviders([]);
      });
    return () => {
      cancelled = true;
    };
  }, [isOpen, selectedMode]);

  if (!isOpen) return null;

  const handleSsoRedirect = (providerId: string) => {
    const challengeUrl = repositoryManager.getHttpRepository().getExternalChallengeUrl(providerId);
    window.location.assign(challengeUrl);
  };

  const handleApply = async () => {
    setIsLoading(true);
    setErrorMessage(null);

    try {
      if (selectedMode === 'connected') {
        const repo = repositoryManager.getRepository();
        await repo.login(email, password);
        repositoryManager.setMode('connected');
      } else {
        repositoryManager.setMode('demo');
      }

      await syncData();
      onClose();
    } catch (err: any) {
      setErrorMessage(err.message || 'Failed to authenticate or connect to API.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-md p-4 animate-in fade-in duration-200">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl max-w-xl w-full overflow-hidden flex flex-col text-slate-100">
        
        {/* Header */}
        <div className="p-6 border-b border-slate-800 bg-gradient-to-r from-slate-900 via-slate-850 to-indigo-950/40">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
                <Sparkles className="w-6 h-6" />
              </div>
              <div>
                <h2 className="text-xl font-bold tracking-tight text-white">Select Runtime Mode</h2>
                <p className="text-xs text-slate-400">Choose between local in-memory simulation or live .NET 10 API</p>
              </div>
            </div>
            {canDismiss && (
              <button
                onClick={onClose}
                aria-label="Close mode selector modal"
                className="text-slate-400 hover:text-white p-1.5 rounded-lg hover:bg-slate-800 transition-colors cursor-pointer focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                ✕
              </button>
            )}
          </div>
        </div>

        {/* Content */}
        <div className="p-6 space-y-4">
          {errorMessage && (
            <div className="flex items-center gap-2 p-3 bg-red-950/40 border border-red-500/30 rounded-xl text-xs text-red-300">
              <AlertCircle className="w-4 h-4 shrink-0 text-red-400" />
              <span>{errorMessage}</span>
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            
            {/* Demo Option */}
            <div
              onClick={() => setSelectedMode('demo')}
              className={`cursor-pointer rounded-xl p-4 border transition-all flex flex-col justify-between ${
                selectedMode === 'demo'
                  ? 'border-emerald-500/50 bg-emerald-500/10 shadow-lg shadow-emerald-500/5 ring-1 ring-emerald-500/30'
                  : 'border-slate-800 bg-slate-850/50 hover:border-slate-700'
              }`}
            >
              <div>
                <div className="flex items-center justify-between mb-3">
                  <div className="p-2 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    <Play className="w-5 h-5 fill-emerald-400/20" />
                  </div>
                  {selectedMode === 'demo' && (
                    <CheckCircle2 className="w-5 h-5 text-emerald-400" />
                  )}
                </div>
                <h3 className="text-sm font-semibold text-white mb-1">Demo Mode (In-Memory)</h3>
                <p className="text-xs text-slate-400 leading-relaxed">
                  Fast client-side simulation with pre-seeded templates. No backend or database required.
                </p>
              </div>
              <div className="mt-4 pt-3 border-t border-slate-800/80 text-[11px] font-medium text-emerald-400/90 flex items-center gap-1.5">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                Zero setup & instant access
              </div>
            </div>

            {/* Connected Option */}
            <div
              onClick={() => setSelectedMode('connected')}
              className={`cursor-pointer rounded-xl p-4 border transition-all flex flex-col justify-between ${
                selectedMode === 'connected'
                  ? 'border-indigo-500/50 bg-indigo-500/10 shadow-lg shadow-indigo-500/5 ring-1 ring-indigo-500/30'
                  : 'border-slate-800 bg-slate-850/50 hover:border-slate-700'
              }`}
            >
              <div>
                <div className="flex items-center justify-between mb-3">
                  <div className="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                    <Server className="w-5 h-5" />
                  </div>
                  {selectedMode === 'connected' && (
                    <CheckCircle2 className="w-5 h-5 text-indigo-400" />
                  )}
                </div>
                <h3 className="text-sm font-semibold text-white mb-1">Connected Mode (Live API)</h3>
                <p className="text-xs text-slate-400 leading-relaxed">
                  Real ASP.NET Core .NET 10 backend, Wolverine CQRS, SQL Server / Postgres & JWT security.
                </p>
              </div>
              <div className="mt-4 pt-3 border-t border-slate-800/80 text-[11px] font-medium text-indigo-400/90 flex items-center gap-1.5">
                <Lock className="w-3 h-3" />
                Requires running backend
              </div>
            </div>

          </div>

          {/* Login form if Connected Mode */}
          {selectedMode === 'connected' && (
            <div className="mt-4 p-4 rounded-xl bg-slate-950/60 border border-slate-800 space-y-3 animate-in fade-in slide-in-from-top-2 duration-150">
              {ssoProviders.length > 0 && (
                <div className="space-y-2 pb-3 border-b border-slate-800">
                  <span className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                    <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
                    SSO Entreprise (OpenID Connect)
                  </span>
                  <div className="grid grid-cols-1 gap-2">
                    {ssoProviders.map((provider) => (
                      <button
                        key={provider.id}
                        type="button"
                        onClick={() => handleSsoRedirect(provider.id)}
                        data-testid={`sso-provider-btn-${provider.id}`}
                        className="w-full px-3 py-2 rounded-lg bg-indigo-600/20 hover:bg-indigo-600/30 border border-indigo-500/40 text-xs font-semibold text-indigo-200 flex items-center justify-center gap-2 cursor-pointer transition-colors"
                      >
                        <ShieldCheck className="w-4 h-4 text-indigo-300" />
                        <span>Continuer avec {provider.name}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                  <LogIn className="w-3.5 h-3.5 text-indigo-400" />
                  Connexion locale / Break-glass
                </span>
                <span className="text-[11px] text-slate-500">Preset accounts:</span>
              </div>

              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => { setEmail('admin@tamam.local'); setPassword('Admin@Tamam2026!'); }}
                  className={`text-[11px] px-2.5 py-1 rounded-lg border transition-all ${
                    email === 'admin@tamam.local'
                      ? 'bg-indigo-600/20 border-indigo-500/40 text-indigo-300'
                      : 'bg-slate-850 border-slate-700 text-slate-400 hover:text-slate-200'
                  }`}
                >
                  Principal Admin
                </button>
                <button
                  type="button"
                  onClick={() => { setEmail('adel.sebti@corp.internal'); setPassword('Password123!'); }}
                  className={`text-[11px] px-2.5 py-1 rounded-lg border transition-all ${
                    email === 'adel.sebti@corp.internal'
                      ? 'bg-indigo-600/20 border-indigo-500/40 text-indigo-300'
                      : 'bg-slate-850 border-slate-700 text-slate-400 hover:text-slate-200'
                  }`}
                >
                  Adel (Admin)
                </button>
                <button
                  type="button"
                  onClick={() => setEmail('karim.khmissi@corp.internal')}
                  className={`text-[11px] px-2.5 py-1 rounded-lg border transition-all ${
                    email === 'karim.khmissi@corp.internal'
                      ? 'bg-indigo-600/20 border-indigo-500/40 text-indigo-300'
                      : 'bg-slate-850 border-slate-700 text-slate-400 hover:text-slate-200'
                  }`}
                >
                  Karim (Finance)
                </button>
              </div>

              <div className="space-y-2">
                <div>
                  <label htmlFor="local-login-email" className="text-[11px] text-slate-400 block mb-1">Email</label>
                  <input
                    id="local-login-email"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-indigo-500"
                    placeholder="user@corp.internal"
                  />
                </div>
                <div>
                  <label htmlFor="local-login-password" className="text-[11px] text-slate-400 block mb-1">Password</label>
                  <input
                    id="local-login-password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full px-3 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white focus:outline-none focus:border-indigo-500"
                    placeholder="••••••••"
                  />
                </div>
              </div>
            </div>
          )}

        </div>

        {/* Footer */}
        <div className="p-4 border-t border-slate-800 bg-slate-950/40 flex justify-end gap-2">
          {canDismiss && (
            <button
              onClick={onClose}
              className="px-4 py-2 text-xs font-medium text-slate-400 hover:text-white rounded-lg transition-colors"
            >
              Cancel
            </button>
          )}
          <button
            onClick={handleApply}
            disabled={isLoading}
            className="px-5 py-2 text-xs font-semibold rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white shadow-lg shadow-indigo-600/20 transition-all flex items-center gap-2 disabled:opacity-50"
          >
            {isLoading ? (
              <span className="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <CheckCircle2 className="w-4 h-4" />
            )}
            {selectedMode === 'connected' ? 'Connect & Enter' : 'Start Demo Session'}
          </button>
        </div>

      </div>
    </div>
  );
};
