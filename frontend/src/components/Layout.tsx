import { NavLink, Outlet } from 'react-router-dom';

export function Layout() {
  return (
    <div className="min-h-screen bg-gray-950 text-gray-100 flex">
      <nav className="w-52 shrink-0 bg-gray-900 border-r border-gray-800 flex flex-col">
        <div className="px-5 py-5 border-b border-gray-800">
          <h1 className="text-sm font-semibold text-gray-100">Recruiter AI</h1>
          <p className="text-xs text-gray-500 mt-0.5">CV Screening Tool</p>
        </div>
        <div className="flex flex-col gap-1 p-3 flex-1">
          <NavItem to="/screening" label="Screening" />
          <NavItem to="/generator" label="Generator" />
        </div>
      </nav>
      <main className="flex-1 overflow-y-auto">
        <Outlet />
      </main>
    </div>
  );
}

function NavItem({ to, label }: { to: string; label: string }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `px-3 py-2 rounded-lg text-sm transition-colors ${
          isActive
            ? 'bg-blue-600 text-white font-medium'
            : 'text-gray-400 hover:text-gray-100 hover:bg-gray-800'
        }`
      }
    >
      {label}
    </NavLink>
  );
}
