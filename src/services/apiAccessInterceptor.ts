/**
 * Notifies the UI when the API answers 403/401 so users get a clear message instead of a silent failure.
 * Installed once from main.tsx; only touches requests going to the API base URL.
 */
export const API_ERROR_EVENT = 'api:access-error';

export interface ApiAccessErrorDetail {
  status: 401 | 403;
  message: string;
}

let installed = false;

export function installApiAccessInterceptor(): void {
  if (installed || typeof window === 'undefined') return;
  installed = true;

  const apiBase = (import.meta.env.VITE_API_BASE_URL as string) || 'http://localhost:5000/api';
  const originalFetch = window.fetch.bind(window);

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const response = await originalFetch(input, init);
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;

    if (url.startsWith(apiBase) && !url.includes('/auth/login')) {
      if (response.status === 403) {
        window.dispatchEvent(new CustomEvent<ApiAccessErrorDetail>(API_ERROR_EVENT, {
          detail: { status: 403, message: 'Access denied: your role does not allow this action. Contact the principal administrator.' }
        }));
      } else if (response.status === 401) {
        window.dispatchEvent(new CustomEvent<ApiAccessErrorDetail>(API_ERROR_EVENT, {
          detail: { status: 401, message: 'Your session has expired or your account was deactivated. Please sign in again.' }
        }));
      }
    }

    return response;
  };
}
