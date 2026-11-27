import React, { useEffect, useRef, useState } from 'react';
import { AlertCircle, CheckCircle2, ShieldCheck } from 'lucide-react';
import { repositoryManager } from '../../services/repositoryManager';
import { useWorkflowStore } from '../../hooks/useWorkflowStore';
import { useSmartFormStore } from '../../store/useSmartFormStore';

interface LoginCallbackHandlerProps {
  onComplete: () => void;
  onOpenLogin: () => void;
}

export const LoginCallbackHandler: React.FC<LoginCallbackHandlerProps> = ({
  onComplete,
  onOpenLogin
}) => {
  const [status, setStatus] = useState<'exchanging' | 'success' | 'error'>('exchanging');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const executedRef = useRef(false);

  const syncDataFromRepository = useWorkflowStore((s) => s.syncDataFromRepository);
  const syncForms = useSmartFormStore((s) => s.syncForms);

  useEffect(() => {
    if (executedRef.current) return;
    executedRef.current = true;

    const params = new URLSearchParams(window.location.search);
    const exchangeCode = params.get('exchangeCode');
    const errorParam = params.get('error');

    // Immediately scrub query parameters from browser URL & history
    window.history.replaceState({}, '', '/dashboard');

    if (errorParam) {
      setStatus('error');
      setErrorMessage(errorParam);
      return;
    }

    if (!exchangeCode) {
      setStatus('error');
      setErrorMessage('Missing SSO exchange code in callback URL.');
      return;
    }

    const completeExchange = async () => {
      try {
        const httpRepo = repositoryManager.getHttpRepository();
        await httpRepo.exchangeExternalAuthCode(exchangeCode);
        repositoryManager.setMode('connected');
        await Promise.all([syncDataFromRepository(), syncForms()]);
        setStatus('success');
        onComplete();
      } catch (err: any) {
        setStatus('error');
        setErrorMessage(err?.message || 'SSO login exchange failed or code expired.');
      }
    };

    void completeExchange();
  }, [onComplete, syncDataFromRepository, syncForms]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-950 text-slate-100 p-6">
      <div className="w-full max-w-md rounded-2xl bg-slate-900 border border-slate-800 shadow-2xl p-6 space-y-4 text-center">
        {status === 'exchanging' && (
          <>
            <div className="mx-auto w-12 h-12 rounded-2xl bg-indigo-500/15 border border-indigo-500/30 flex items-center justify-center text-indigo-400">
              <ShieldCheck className="w-6 h-6 animate-pulse" />
            </div>
            <h1 className="text-lg font-bold text-white">Finalisation de la connexion SSO...</h1>
            <p className="text-xs text-slate-400">
              Échange sécurisé du code éphémère en cours. Veuillez patienter.
            </p>
          </>
        )}

        {status === 'success' && (
          <>
            <div className="mx-auto w-12 h-12 rounded-2xl bg-emerald-500/15 border border-emerald-500/30 flex items-center justify-center text-emerald-400">
              <CheckCircle2 className="w-6 h-6" />
            </div>
            <h1 className="text-lg font-bold text-white">Connexion réussie</h1>
            <p className="text-xs text-slate-400">Redirection vers votre espace de travail...</p>
          </>
        )}

        {status === 'error' && (
          <>
            <div className="mx-auto w-12 h-12 rounded-2xl bg-red-500/15 border border-red-500/30 flex items-center justify-center text-red-400">
              <AlertCircle className="w-6 h-6" />
            </div>
            <h1 className="text-lg font-bold text-white">Échec de la connexion SSO</h1>
            <div
              role="alert"
              className="p-3 rounded-xl bg-red-950/50 border border-red-500/30 text-xs text-red-300 text-start"
            >
              {errorMessage}
            </div>
            <div className="flex justify-center gap-2 pt-2">
              <button
                type="button"
                onClick={() => {
                  onComplete();
                  onOpenLogin();
                }}
                className="px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-xs font-semibold text-white cursor-pointer transition-colors"
              >
                Retour à la connexion
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
};
