using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates a minimal playable scene for the development project.
/// Can also be invoked in batch mode with -executeMethod CreateDevScene.Create.
/// </summary>
public static class CreateDevScene
{
    [MenuItem("Tools/UnityHFSM/Create Dev Scene")]
    public static void Create()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = new GameObject("Main Camera");
        camera.tag = "MainCamera";
        camera.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -10f);

        var go = new GameObject("FSM Playground");
        go.AddComponent<FsmPlayground>();

        const string folder = "Assets/Dev/Scenes";
        Directory.CreateDirectory(folder);
        EditorSceneManager.SaveScene(scene, folder + "/Playground.unity");
        AssetDatabase.Refresh();
    }
}
