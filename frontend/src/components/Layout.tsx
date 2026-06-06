import { NavLink, Outlet } from 'react-router-dom';
import type { ReactNode } from 'react';
import { features } from '../config/features';

// Inline SVGs (no icon package) — small, consistent stroke width, currentColor.
const icons = {
  positions: (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-4 h-4">
      <path d="M3 5h14M3 10h14M3 15h9" strokeLinecap="round" />
    </svg>
  ),
  candidates: (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-4 h-4">
      <circle cx="10" cy="7" r="3" />
      <path d="M4 17c0-3 2.7-5 6-5s6 2 6 5" strokeLinecap="round" />
    </svg>
  ),
  screening: (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-4 h-4">
      <circle cx="9" cy="9" r="5" />
      <path d="M13 13l4 4" strokeLinecap="round" />
    </svg>
  ),
  generator: (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.6" className="w-4 h-4">
      <path d="M10 3v3M10 14v3M3 10h3M14 10h3M5.5 5.5l2 2M12.5 12.5l2 2M5.5 14.5l2-2M12.5 7.5l2-2" strokeLinecap="round" />
    </svg>
  ),
};

const CORE_NAV = [
  { to: '/positions',  label: 'Positions',  hint: 'Create & edit positions',    icon: icons.positions  },
  { to: '/candidates', label: 'Candidates', hint: 'Upload & manage candidates', icon: icons.candidates },
  { to: '/screening',  label: 'Screening',  hint: 'Evaluate candidates',        icon: icons.screening  },
];

// Generator is dev/QA tooling — hidden in production via feature flag.
const DEV_NAV = features.syntheticCvGenerator
  ? [{ to: '/generator', label: 'Generator', hint: 'Test screening quality', icon: icons.generator }]
  : [];

export function Layout() {
  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 flex flex-col md:flex-row">
      {/* Desktop sidebar: hidden on mobile, visible from md up. */}
      <nav className="hidden md:flex w-64 shrink-0 bg-gray-900 border-r border-gray-800 flex-col">
        <div className="px-5 py-5 border-b border-gray-800 flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-blue-600/20 border border-blue-500/40 flex items-center justify-center text-blue-400 text-sm font-bold">
            R
          </div>
          <div>
            <h1 className="text-sm font-semibold text-gray-100 leading-tight">Recruiter AI</h1>
            <p className="text-xs text-gray-500">CV Screening Tool</p>
          </div>
        </div>
        <div className="flex flex-col gap-1 p-3 flex-1">
          {CORE_NAV.map(n => (
            <SidebarItem key={n.to} {...n} />
          ))}
          {DEV_NAV.length > 0 && (
            <>
              <div className="mt-3 mb-1 px-3 flex items-center gap-2">
                <span className="text-[10px] font-semibold uppercase tracking-widest text-gray-600">
                  Dev tools
                </span>
                <div className="flex-1 h-px bg-gray-800" />
              </div>
              {DEV_NAV.map(n => (
                <SidebarItem key={n.to} {...n} devTool />
              ))}
            </>
          )}
        </div>
        <div className="px-4 py-3 border-t border-gray-800 text-[11px] text-gray-600">
          Portfolio demo · v0.1
        </div>
      </nav>

      {/* Mobile top bar: brand only — saves screen real estate. */}
      <header className="md:hidden flex items-center gap-3 px-4 py-3 bg-gray-900 border-b border-gray-800">
        <div className="w-7 h-7 rounded-lg bg-blue-600/20 border border-blue-500/40 flex items-center justify-center text-blue-400 text-xs font-bold">
          R
        </div>
        <div className="leading-tight">
          <h1 className="text-sm font-semibold text-gray-100">Recruiter AI</h1>
          <p className="text-[10px] text-gray-500">CV Screening Tool</p>
        </div>
      </header>

      {/* Main scroll area — extra bottom padding on mobile to clear the fixed bottom nav. */}
      <main className="flex-1 overflow-y-auto pb-20 md:pb-0">
        <Outlet />
      </main>

      {/* Mobile bottom nav: fixed, four equal pills. */}
      <nav
        className="md:hidden fixed bottom-0 inset-x-0 z-40 bg-gray-900 border-t border-gray-800 flex justify-around px-2 py-2"
        style={{ paddingBottom: 'max(env(safe-area-inset-bottom), 0.5rem)' }}
      >
        {CORE_NAV.map(n => (
          <BottomNavItem key={n.to} to={n.to} label={n.label} icon={n.icon} />
        ))}
        {DEV_NAV.map(n => (
          <BottomNavItem key={n.to} to={n.to} label={n.label} icon={n.icon} devTool />
        ))}
      </nav>
    </div>
  );
}

function SidebarItem({
  to, label, hint, icon, devTool = false,
}: { to: string; label: string; hint: string; icon: ReactNode; devTool?: boolean }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `flex items-start gap-2.5 px-3 py-2 rounded-lg transition-colors ${
          isActive
            ? devTool
              ? 'bg-amber-600/20 text-amber-200 shadow-sm'
              : 'bg-blue-600 text-white shadow-sm shadow-blue-900/30'
            : devTool
              ? 'text-amber-500/70 hover:text-amber-300 hover:bg-amber-900/20'
              : 'text-gray-400 hover:text-gray-100 hover:bg-gray-800'
        }`
      }
    >
      {({ isActive }) => (
        <>
          <span className="opacity-90 mt-0.5">{icon}</span>
          <span className="flex flex-col leading-tight min-w-0">
            <span className={`text-sm flex items-center gap-1.5 ${isActive ? 'font-medium' : ''}`}>
              {label}
              {devTool && (
                <span className="text-[9px] font-bold uppercase tracking-wider px-1 py-0.5 rounded bg-amber-900/40 text-amber-400 border border-amber-700/40 leading-none">
                  dev
                </span>
              )}
            </span>
            <span className={`text-[11px] ${
              isActive
                ? devTool ? 'text-amber-200/70' : 'text-blue-100/90'
                : 'text-gray-500'
            }`}>
              {hint}
            </span>
          </span>
        </>
      )}
    </NavLink>
  );
}

function BottomNavItem({
  to, label, icon, devTool = false,
}: { to: string; label: string; icon: ReactNode; devTool?: boolean }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `flex-1 flex flex-col items-center gap-0.5 py-1.5 rounded-lg transition-colors ${
          isActive
            ? devTool ? 'text-amber-400 bg-amber-600/15' : 'text-blue-400 bg-blue-600/15'
            : devTool ? 'text-amber-600/60 hover:text-amber-300' : 'text-gray-500 hover:text-gray-200'
        }`
      }
    >
      <span className="opacity-90">{icon}</span>
      <span className="text-[10px] font-medium">{label}</span>
    </NavLink>
  );
}
