import { useEffect, useState } from 'react';
import { TemplateApi } from '../epicApi';

export default function TemplatesPage() {
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    TemplateApi.getGovernance()
      .then(text => { setContent(text); setDirty(false); })
      .catch(e => alert(String(e)))
      .finally(() => setLoading(false));
  }, []);

  async function handleSave() {
    setSaving(true);
    try {
      await TemplateApi.saveGovernance(content);
      setDirty(false);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      alert(String(e));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="min-h-screen bg-zinc-950 flex flex-col">
      <div className="max-w-4xl mx-auto w-full px-6 py-6 flex-1 flex flex-col">

        <div className="flex items-center justify-between mb-4">
          <div>
            <h1 className="text-base font-bold text-zinc-100">Governance Template</h1>
            <p className="text-xs text-zinc-500 mt-0.5">This file is copied into every new epic as its governance.md.</p>
          </div>
          <div className="flex items-center gap-2">
            {saved && <span className="text-xs text-emerald-400">Saved</span>}
            <button
              onClick={handleSave}
              disabled={saving || !dirty}
              className="text-sm font-semibold px-4 py-1.5 rounded-lg bg-indigo-600 text-white hover:bg-indigo-500 disabled:opacity-40 transition-colors"
            >
              {saving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </div>

        <div className="flex-1 bg-zinc-900 border border-zinc-800 rounded-xl overflow-hidden flex flex-col">
          {loading ? (
            <p className="text-sm text-zinc-500 p-5">Loading…</p>
          ) : (
            <textarea
              value={content}
              onChange={e => { setContent(e.target.value); setDirty(true); setSaved(false); }}
              className="flex-1 w-full bg-transparent text-xs font-mono text-zinc-200 p-5 resize-none focus:outline-none leading-relaxed min-h-[70vh]"
              spellCheck={false}
            />
          )}
        </div>

      </div>
    </div>
  );
}
