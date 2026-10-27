using UnityEngine;

public abstract class BaseGameScene
{
    public readonly GameSceneConfigSO gameSceneConfig;

    public BaseGameScene(GameSceneConfigSO so)
    {
        gameSceneConfig = so;
    }

    public void OnClientLoadScene() { }
    public void OnHostLoadScene() { }
}

[CreateAssetMenu(menuName = "Configs/Game Scene Config")]
public class GameSceneConfigSO : ScriptableObject
{
    public string UnitySceneName;
    public string LoadingSceneDescription;

}
