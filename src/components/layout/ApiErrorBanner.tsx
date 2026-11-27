import React, { useEffect, useState } from 'react';
import { API_ERROR_EVENT, type ApiAccessErrorDetail } from '../../services/apiAccessInterceptor';

/** Global, accessible banner shown when the API denies access (403) or the session is invalid (401). */
export const ApiErrorBanner: React.FC = () => {
  const [detail, setDetail] = useState<ApiAccessErrorDetail | null>(null);

  useEffect(() => {
    const onError = (e: Event) => setDetail((e as CustomEvent<ApiAccessErrorDetail>).detail);
    window.addEventListener(API_ERROR_EVENT, onError);
    return () => window.removeEventListener(API_ERROR_EVENT, onError);
  }, []);

  useEffect(() => {
    if (!detail) return;
    const timer = window.setTimeout(() => setDetail(null), 8000);
    return () => window.clearTimeout(timer);
  }, [detail]);

  if (!detail) return null;

  return (
    <div
      role="alert"
      data-testid="api-error-banner"
      className="fixed top-4 inset-x-0 z-[100] mx-auto max-w-xl px-4"
    >
      <div className="flex items-start gap-3 rounded-xl border border-red-300 bg-red-50 px-4 py-3 text-xs font-medium text-red-700 shadow-lg dark:border-red-900 dark:bg-red-950 dark:text-red-300">
        <span className="flex-1">{detail.message}</span>
        <button
          type="button"
          aria-label="Dismiss"
          onClick={() => setDetail(null)}
          className="cursor-pointer font-bold"
        >
          ✕
        </button>
      </div>
    </div>
  );
};
