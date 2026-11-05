using Godot;

namespace PixelBlock.Combat;

public interface ICharacterModel
{
    void Animate(float speed, float dt);
    void Attack();
    void Squash();
    void SetFlash(bool on);
}

public static class CharacterModel
{
    public static Node3D BuildHumanoid(Color skin, Color cloth, Color accent,
                                       string modelScene = null, float modelYaw = 0f)
    {
        Node3D rigged = RiggedModel.TryLoad(modelScene, modelYaw);
        return rigged ?? MobModel.BuildHumanoid(skin, cloth, accent);
    }

    public static Node3D BuildBeast(Color fur, Color belly,
                                    string modelScene = null, float modelYaw = 0f)
    {
        Node3D rigged = RiggedModel.TryLoad(modelScene, modelYaw);
        return rigged ?? MobModel.BuildBeast(fur, belly);
    }
}
