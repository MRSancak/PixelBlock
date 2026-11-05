using System.Collections.Generic;
using Godot;

namespace PixelBlock.Combat;

public partial class RiggedModel : Node3D, ICharacterModel
{
    private Node3D _inner;
    private AnimationPlayer _anim;
    private readonly List<StandardMaterial3D> _flashMats = new();
    private string _idle, _walk, _attack;
    private string _current;
    private bool _attacking;

    public float FacingOffsetDeg
    {
        set { if (_inner != null) _inner.Rotation = new Vector3(0, Mathf.DegToRad(value), 0); }
    }

    public static RiggedModel TryLoad(string scenePath, float facingDeg = 0f)
    {
        if (string.IsNullOrEmpty(scenePath)) return null;
        if (!ResourceLoader.Exists(scenePath))
        {
            GD.PushWarning($"[RA] model scene not found, using box model instead: {scenePath}");
            return null;
        }
        var root = ResourceLoader.Load<PackedScene>(scenePath)?.Instantiate<Node3D>();
        if (root == null) return null;
        var m = new RiggedModel { Name = "Model" };
        if (!m.Init(root)) { root.QueueFree(); m.QueueFree(); return null; }
        m.FacingOffsetDeg = facingDeg;
        return m;
    }

    public bool Init(Node3D root)
    {
        _inner = root;
        AddChild(_inner);
        _anim = FindAnim(_inner);
        if (_anim == null) return false;

        CollectFlashMaterials(_inner);
        var clips = new List<string>(_anim.GetAnimationList());
        _idle = Pick(clips, "idle") ?? (clips.Count > 0 ? clips[0] : null);
        _walk = Pick(clips, "walk", "run", "move") ?? _idle;
        _attack = Pick(clips, "attack", "melee", "swing", "punch", "chop", "slash") ?? _idle;
        ForceLoop(_idle);
        ForceLoop(_walk);
        if (_idle != null) { _anim.Play(_idle); _current = _idle; }
        return true;
    }

    public void Animate(float speed, float dt)
    {
        if (_anim == null || _attacking) return;
        string want = speed > 0.6f ? _walk : _idle;
        if (want != null && want != _current) { _anim.Play(want); _current = want; }
    }

    public void Attack()
    {
        if (_anim == null || _attack == null || _attacking) return;
        _attacking = true;
        _anim.AnimationFinished += OnAttackFinished;
        _anim.Play(_attack);
    }

    private void OnAttackFinished(StringName which)
    {
        if ((string)which != _attack) return;
        _anim.AnimationFinished -= OnAttackFinished;
        _attacking = false;
        _current = null;
    }

    public void Squash()
    {
        if (_inner == null || !IsInsideTree()) return;
        _inner.Scale = Vector3.One;
        var t = CreateTween();
        t.TweenProperty(_inner, "scale", new Vector3(1.12f, 0.88f, 1.12f), 0.05);
        t.TweenProperty(_inner, "scale", Vector3.One, 0.12).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    public void SetFlash(bool on)
    {
        Color c = on ? new Color(0.9f, 0.15f, 0.12f) : Colors.Black;
        foreach (StandardMaterial3D m in _flashMats)
        {
            m.EmissionEnabled = on;
            m.Emission = c;
        }
    }

    private static AnimationPlayer FindAnim(Node n)
    {
        if (n is AnimationPlayer ap) return ap;
        foreach (Node c in n.GetChildren())
        {
            var f = FindAnim(c);
            if (f != null) return f;
        }
        return null;
    }

    private void CollectFlashMaterials(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                if (mi.GetActiveMaterial(s) is StandardMaterial3D src)
                {
                    var mat = (StandardMaterial3D)src.Duplicate();
                    mi.SetSurfaceOverrideMaterial(s, mat);
                    _flashMats.Add(mat);
                }
        }
        foreach (Node c in n.GetChildren()) CollectFlashMaterials(c);
    }

    private static string Pick(List<string> clips, params string[] keys)
    {
        foreach (string k in keys)
            foreach (string c in clips)
                if (c.ToLower().Contains(k)) return c;
        return null;
    }

    private void ForceLoop(string clip)
    {
        if (clip == null) return;
        var a = _anim.GetAnimation(clip);
        if (a != null) a.LoopMode = Animation.LoopModeEnum.Linear;
    }
}
