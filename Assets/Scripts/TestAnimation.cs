using UnityEngine;
using DragonBones;

public class DragonBonesLoader : MonoBehaviour {
    void Start() {
        try {
            Debug.Log("Attempting to load SKE...");
            var skeData = UnityFactory.factory.LoadDragonBonesData("Naboo/Naboo_ske");
            
            Debug.Log("Attempting to load TEX...");
            // Use the internal name "Naboo_ske" you found in your JSON
            UnityFactory.factory.LoadTextureAtlasData("Naboo/Naboo_tex", "Naboo_ske");

            Debug.Log("Attempting to build Armature...");
            var armatureComponent = UnityFactory.factory.BuildArmatureComponent("armature1");

            if (armatureComponent != null) {
                Debug.Log("Success! Playing animation.");
                armatureComponent.animation.Play("Naboo Run");
            }
        }
        catch (System.Exception e) {
            Debug.LogError("The DragonBones library crashed! Error: " + e.Message + "\nStack Trace: " + e.StackTrace);
        }
    }
}