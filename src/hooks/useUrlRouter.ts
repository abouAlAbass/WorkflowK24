import { useEffect, useRef } from 'react';
import type { NavigationTab } from '../types/workflow';
import { useWorkflowStore } from './useWorkflowStore';

const VALID_TABS: NavigationTab[] = [
  'dashboard',
  'portal',
  'designer',
  'smartforms',
  'instances',
  'smartobjects',
  'tasks',
  'analytics',
  'users',
  'settings'
];

export interface ParsedRoute {
  tab: NavigationTab;
  entityId?: string;
}

export function parsePathname(pathname: string): ParsedRoute {
  const segments = pathname
    .split('/')
    .map((s) => s.trim())
    .filter(Boolean);

  if (segments.length === 0) {
    return { tab: 'dashboard' };
  }

  const first = segments[0].toLowerCase() as NavigationTab;
  if (VALID_TABS.includes(first)) {
    return {
      tab: first,
      entityId: segments[1] ? decodeURIComponent(segments[1]) : undefined
    };
  }

  return { tab: 'dashboard' };
}

export function buildPathForTab(tab: NavigationTab, entityId?: string): string {
  if (tab === 'dashboard' && !entityId) {
    return '/dashboard';
  }
  return entityId ? `/${tab}/${encodeURIComponent(entityId)}` : `/${tab}`;
}

/**
 * Synchronizes browser URL paths (/tasks/:id, /instances/:id, /designer/:workflowKey, etc.)
 * bidirectionally with Zustand navigation state using the HTML5 History API.
 */
export function useUrlRouter(): void {
  const currentTab = useWorkflowStore((s) => s.currentTab);
  const setCurrentTab = useWorkflowStore((s) => s.setCurrentTab);
  const savedWorkflows = useWorkflowStore((s) => s.savedWorkflows);
  const loadWorkflowFromDb = useWorkflowStore((s) => s.loadWorkflowFromDb);

  const initializedRef = useRef(false);
  const suppressNextPushRef = useRef(false);

  // 1. Hydrate initial tab & deep-linked workflow on mount + listen to browser Back/Forward
  useEffect(() => {
    if (typeof window === 'undefined') return;

    const applyCurrentLocation = () => {
      const { tab, entityId } = parsePathname(window.location.pathname);
      suppressNextPushRef.current = true;
      setCurrentTab(tab);

      if (tab === 'designer' && entityId && typeof loadWorkflowFromDb === 'function') {
        const matched = savedWorkflows.find((w) => w.id === entityId);
        if (matched) {
          loadWorkflowFromDb(matched.id);
        }
      }
    };

    if (!initializedRef.current) {
      initializedRef.current = true;
      if (
        window.location.pathname &&
        window.location.pathname !== '/' &&
        window.location.pathname !== '/login/callback'
      ) {
        applyCurrentLocation();
      }
    }

    const handlePopState = () => {
      applyCurrentLocation();
    };

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [setCurrentTab, savedWorkflows, loadWorkflowFromDb]);

  // 2. Push clean URL whenever currentTab changes via UI navigation
  useEffect(() => {
    if (typeof window === 'undefined' || !initializedRef.current) return;

    if (suppressNextPushRef.current) {
      suppressNextPushRef.current = false;
      return;
    }

    const currentParsed = parsePathname(window.location.pathname);
    if (currentParsed.tab !== currentTab) {
      const targetPath = buildPathForTab(currentTab);
      window.history.pushState({ tab: currentTab }, '', targetPath);
    }
  }, [currentTab]);
}
