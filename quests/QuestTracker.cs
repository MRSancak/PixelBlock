using System.Collections.Generic;

namespace PixelBlock.Quests;

public sealed class QuestTracker
{
    private readonly GameSession _s;
    private readonly Quest _quest;
    private readonly IReadOnlyList<Objective> _objs;
    private readonly int[] _progress;
    private readonly bool[] _done;
    private readonly HashSet<string>[] _seen;
    private bool _completed;

    public QuestTracker(GameSession s, Quest quest)
    {
        _s = s;
        _quest = quest;
        _objs = quest.Objectives;
        int n = _objs.Count;
        _progress = new int[n];
        _done = new bool[n];
        _seen = new HashSet<string>[n];
        for (int i = 0; i < n; i++) _seen[i] = new HashSet<string>();
    }

    public void Begin() => Render();

    public void OnTalk(string npcName)
    {
        for (int i = 0; i < _objs.Count; i++)
        {
            var o = _objs[i];
            if (_done[i] || o.Kind != ObjectiveKind.Talk) continue;
            if (o.Key != null && o.Key != npcName) continue;
            if (!_seen[i].Add(npcName ?? "")) continue;
            Advance(i);
            return;
        }
    }

    public void OnDefeat(string enemyTypeName)
    {
        for (int i = 0; i < _objs.Count; i++)
        {
            var o = _objs[i];
            if (_done[i] || o.Kind != ObjectiveKind.Defeat) continue;
            if (o.Key != null && !string.Equals(o.Key, enemyTypeName, System.StringComparison.OrdinalIgnoreCase)) continue;
            Advance(i);
            return;
        }
    }

    public void OnReach(string triggerId)
    {
        for (int i = 0; i < _objs.Count; i++)
        {
            var o = _objs[i];
            if (_done[i] || o.Kind != ObjectiveKind.Reach) continue;
            if (o.Key != null && o.Key != triggerId) continue;
            if (!_seen[i].Add(triggerId ?? "")) continue;
            Advance(i);
            return;
        }
    }

    public void OnBlockChanged(ushort oldId, ushort newId)
    {
        for (int i = 0; i < _objs.Count; i++)
        {
            var o = _objs[i];
            if (_done[i]) continue;
            if (o.Kind != ObjectiveKind.Break && o.Kind != ObjectiveKind.Place && o.Kind != ObjectiveKind.Collect)
                continue;

            if (!Core.BlockRegistry.TryId(o.Key, out ushort target) || target == 0) continue;

            bool match = o.Kind == ObjectiveKind.Place
                ? (newId == target && oldId != target)
                : (oldId == target && newId == 0);
            if (!match) continue;
            Advance(i);
            return;
        }
    }

    public int Progress(int i) => (i >= 0 && i < _progress.Length) ? _progress[i] : 0;
    public bool IsDone(int i) => i >= 0 && i < _done.Length && _done[i];
    public bool AllDone
    {
        get
        {
            for (int i = 0; i < _done.Length; i++)
                if (!_done[i] && !_objs[i].Optional) return false;
            return true;
        }
    }

    private void Advance(int i)
    {
        var o = _objs[i];
        _progress[i]++;
        if (_progress[i] < o.Count)
        {
            Render();
            return;
        }

        _done[i] = true;
        Core.AudioManager.Play("chime");
        Render();
        o.OnComplete?.Invoke(_s);
        if (AllDone && !_completed)
        {
            _completed = true;
            _quest.OnComplete?.Invoke(_s);
            _s.NotifyQuestComplete();
        }
    }

    private void Render()
    {
        var lines = new List<(string, bool)>(_objs.Count);
        for (int i = 0; i < _objs.Count; i++)
        {
            var o = _objs[i];
            string text = o.Counted
                ? $"{o.Label} ({System.Math.Min(_progress[i], o.Count)}/{o.Count})"
                : o.Label;
            lines.Add((text, _done[i]));
        }
        _s.Hud.SetObjectivesWithState(lines);
    }
}
