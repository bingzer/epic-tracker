import { useEffect, useState } from 'react';
import { NavLink, Route, Routes } from 'react-router-dom';
import { useSignalR } from './hooks/useSignalR';
import EpicsListPage from './pages/EpicsListPage';
import EpicDetailPage from './pages/EpicDetailPage';
import TemplatesPage from './pages/TemplatesPage';

function useDarkMode() {
  const [dark, setDark] = useState(() => {
    const saved = localStorage.getItem('theme');
    return saved ? saved === 'dark' : window.matchMedia('(prefers-color-scheme: dark)').matches;
  });

  useEffect(() => {
    document.documentElement.classList.toggle('dark', dark);
    localStorage.setItem('theme', dark ? 'dark' : 'light');
  }, [dark]);

  return [dark, () => setDark(d => !d)] as const;
}

const navClass = ({ isActive }: { isActive: boolean }) =>
  `px-3 py-1.5 text-sm font-medium rounded-md transition-colors ${
    isActive
      ? 'bg-gray-100 dark:bg-zinc-800 text-gray-900 dark:text-zinc-100'
      : 'text-gray-500 dark:text-zinc-400 hover:text-gray-700 dark:hover:text-zinc-200 hover:bg-gray-50 dark:hover:bg-zinc-800/50'
  }`;

const isDev = location.port === '6791';

export default function App() {
  const [dark, toggleDark] = useDarkMode();

  const connected = useSignalR('/hubs/epic', {});

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-zinc-950 flex flex-col">
      <nav className="bg-white dark:bg-zinc-900 border-b border-gray-200 dark:border-zinc-800 h-12 flex items-center gap-1 px-5">
        <span className="text-sm font-semibold text-gray-900 dark:text-zinc-100 mr-3">Epic Tracker</span>
        {isDev && (
          <span className="text-xs font-medium bg-sky-100 text-sky-600 dark:bg-sky-900/40 dark:text-sky-400 px-1.5 py-0.5 rounded mr-2">DEV</span>
        )}
        <NavLink to="/" end className={navClass}>Epics</NavLink>
        <NavLink to="/templates" className={navClass}>Templates</NavLink>

        <div className="flex items-center gap-2 ml-auto">
          <span className={`w-2 h-2 rounded-full ${connected ? 'bg-emerald-500' : 'bg-red-400'}`} />
          <span className="text-xs text-gray-500 dark:text-zinc-500">{connected ? 'Live' : 'Offline'}</span>
          <button
            onClick={toggleDark}
            className="text-gray-400 dark:text-zinc-500 hover:text-gray-600 dark:hover:text-zinc-300 transition-colors p-1.5 rounded-md hover:bg-gray-100 dark:hover:bg-zinc-800"
            aria-label="Toggle dark mode"
          >
            {dark ? '☀️' : '🌙'}
          </button>
        </div>
      </nav>

      <main className="flex-1 min-h-0">
        <Routes>
          <Route path="/" element={<EpicsListPage />} />
          <Route path="/epics/:epicId" element={<EpicDetailPage />} />
          <Route path="/templates" element={<TemplatesPage />} />
        </Routes>
      </main>
    </div>
  );
}

