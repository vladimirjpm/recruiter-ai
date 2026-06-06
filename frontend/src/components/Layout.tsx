import { NavLink, Outlet } from 'react-router-dom';
import type { ReactNode } from 'react';

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

export function Layout() {
  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 flex">
      <nav className="w-64 shrink-0 bg-gray-900 border-r border-gray-800 flex flex-col">
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
          <NavItem to="/positions"  label="Positions"  hint="Create & edit positions"     icon={icons.positions}  />
          <NavItem to="/candidates" label="Candidates" hint="Upload & manage candidates"  icon={icons.candidates} />
          <NavItem to="/screening"  label="Screening"  hint="Evaluate candidates"         icon={icons.screening}  />
          <NavItem to="/generator"  label="Generator"  hint="Generate synthetic CVs"      icon={icons.generator}  />
        </div>
        <div className="px-4 py-3 border-t border-gray-800 text-[11px] text-gray-600">
          Portfolio demo · v0.1
        </div>
      </nav>
      <main className="flex-1 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}

function NavItem({
  to, label, hint, icon,
}: { to: string; label: string; hint: string; icon: ReactNode }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `flex items-start gap-2.5 px-3 py-2 rounded-lg transition-colors ${
          isActive
            ? 'bg-blue-600 text-white shadow-sm shadow-blue-900/30'
            : 'text-gray-400 hover:text-gray-100 hover:bg-gray-800'
        }`
      }
    >
      {({ isActive }) => (
        <>
          <span className="opacity-90 mt-0.5">{icon}</span>
          <span className="flex flex-col leading-tight">
            <span className={`text-sm ${isActive ? 'font-medium' : ''}`}>{label}</span>
            <span className={`text-[11px] ${isActive ? 'text-blue-100/90' : 'text-gray-500'}`}>
              {hint}
            </span>
          </span>
        </>
      )}
    </NavLink>
  );
}
