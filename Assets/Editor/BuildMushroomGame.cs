using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildMushroomGame
{
    const string ScenePath="Assets/Scenes/Forest.unity";
    static readonly string[] Trees={"Assets/ImportedModels/FBX/BirchTree_1.fbx","Assets/ImportedModels/FBX/BirchTree_2.fbx","Assets/ImportedModels/FBX/CommonTree_1.fbx","Assets/ImportedModels/FBX/CommonTree_2.fbx","Assets/ImportedModels/FBX/PineTree_1.fbx","Assets/ImportedModels/FBX/PineTree_2.fbx"};
    static readonly string[] Rocks={"Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_001.fbx","Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_002.fbx","Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_003.fbx","Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_Small_001.fbx","Assets/ImportedModels/EEE - Low Poly Rock Pack 001/FBX/Low_Poly_Rock_Boulder.fbx"};
    static readonly string[] Details={"Assets/ImportedModels/FBX/Bush_1.fbx","Assets/ImportedModels/FBX/Bush_2.fbx","Assets/ImportedModels/FBX/Flowers.fbx","Assets/ImportedModels/FBX/WoodLog.fbx"};
    static readonly List<Vector3> obstaclePositions=new List<Vector3>();
    static GameObject Load(string p){var a=AssetDatabase.LoadAssetAtPath<GameObject>(p);if(!a)Debug.LogWarning("Imported model not found: "+p);return a;}
    static void Tag(string t){var m=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var p=m.FindProperty("tags");for(int i=0;i<p.arraySize;i++)if(p.GetArrayElementAtIndex(i).stringValue==t)return;p.InsertArrayElementAtIndex(p.arraySize);p.GetArrayElementAtIndex(p.arraySize-1).stringValue=t;m.ApplyModifiedProperties();}
    static Material Pbr(){var m=new Material(Shader.Find("Standard")){name="ForestGround"};var a=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Ground_Diffuse.jpg");var n=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Ground_nor_gl.jpg");if(a)m.mainTexture=a;if(n)m.SetTexture("_BumpMap",n);m.mainTextureScale=new Vector2(18,18);m.SetTextureScale("_BumpMap",new Vector2(18,18));m.SetFloat("_Metallic",0f);m.SetFloat("_Glossiness",.18f);return m;}
    static Bounds BoundsOf(GameObject o){var rs=o.GetComponentsInChildren<Renderer>(true);var b=new Bounds(o.transform.position,Vector3.zero);foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void MakeUpright(GameObject o)
    {
        var b=BoundsOf(o);
        if(b.size.y >= Mathf.Max(b.size.x,b.size.z)*.7f) return;
        if(b.size.x >= b.size.z) o.transform.rotation*=Quaternion.Euler(0f,0f,90f);
        else o.transform.rotation*=Quaternion.Euler(90f,0f,0f);
    }
    static void PlaceOnGround(GameObject o)
    {
        var b=BoundsOf(o);
        o.transform.position += Vector3.up * (o.transform.position.y - b.min.y);
    }
    static void Scale(GameObject o,float h){var b=BoundsOf(o);if(b.size.y>.01f)o.transform.localScale*=h/b.size.y;}
    static GameObject Spawn(string p,Vector3 pos,float h,string n){var a=Load(p);if(!a)return null;var o=new GameObject(n);o.transform.position=pos;o.transform.rotation=Quaternion.Euler(0,Random.Range(0,360),0);var visual=Object.Instantiate(a,o.transform,false);visual.name="Visual";return o;}
    static void AddTrunkCollider(GameObject o){if(o.GetComponentInChildren<Collider>())return;float s=Mathf.Max(.01f,o.transform.lossyScale.x);var c=o.AddComponent<CapsuleCollider>();c.center=new Vector3(0,(1.1f-o.transform.position.y)/s,0);c.radius=.45f/s;c.height=2.2f/s;c.isTrigger=false;}
    static void AddBoundsCollider(GameObject o,bool trigger){if(o.GetComponentInChildren<Collider>())return;var r=o.GetComponentsInChildren<Renderer>();if(r.Length==0)return;var b=r[0].bounds;for(int i=1;i<r.Length;i++)b.Encapsulate(r[i].bounds);var c=o.AddComponent<BoxCollider>();c.center=o.transform.InverseTransformPoint(b.center);c.size=new Vector3(b.size.x/Mathf.Max(.01f,o.transform.lossyScale.x),b.size.y/Mathf.Max(.01f,o.transform.lossyScale.y),b.size.z/Mathf.Max(.01f,o.transform.lossyScale.z));c.isTrigger=trigger;}
    static void KeepOneMushroom(GameObject o){var visual=o.transform.Find("Visual");if(!visual)return;var candidates=new List<Transform>();foreach(Transform child in visual){if(child.GetComponentsInChildren<Renderer>(true).Length>0)candidates.Add(child);}if(candidates.Count==0)return;var keep=candidates[Random.Range(0,candidates.Count)];foreach(var child in candidates)if(child!=keep)Object.DestroyImmediate(child.gameObject);keep.localPosition=Vector3.zero;}
    static void Mushroom(Vector3 p){var o=Spawn("Assets/ImportedModels/MushroomPack/Mushrooms.fbx",p,1f,"Mushroom");if(o){KeepOneMushroom(o);Scale(o,.9f);PlaceOnGround(o);o.tag="Mushroom";AddBoundsCollider(o,true);o.AddComponent<MushroomPickup>();}}
    static void Tree(Vector3 p,Transform par){var o=Spawn(Trees[Random.Range(0,Trees.Length)],p,1f,"Tree");if(o){o.transform.SetParent(par,true);Scale(o,Random.Range(7,11));PlaceOnGround(o);AddTrunkCollider(o);obstaclePositions.Add(p);}}
    static void Rock(Vector3 p,Transform par){var o=Spawn(Rocks[Random.Range(0,Rocks.Length)],p,1f,"Rock");if(o){o.transform.SetParent(par,true);Scale(o,Random.Range(.45f,1.4f));PlaceOnGround(o);AddBoundsCollider(o,false);obstaclePositions.Add(p);}}
    static void Detail(Vector3 p,Transform par){var o=Spawn(Details[Random.Range(0,Details.Length)],p,1f,"ForestDetail");if(o){o.transform.SetParent(par,true);Scale(o,Random.Range(.25f,1.4f));PlaceOnGround(o);obstaclePositions.Add(p);}}
    static Transform FindChild(Transform root,string name){if(!root)return null;if(root.name==name)return root;foreach(Transform child in root){var found=FindChild(child,name);if(found)return found;}return null;}
    static void CreateShadowPart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler)
    {
        var part=GameObject.CreatePrimitive(type);
        part.name=name;
        part.transform.SetParent(parent,false);
        part.transform.localPosition=position;
        part.transform.localScale=scale;
        part.transform.localEulerAngles=euler;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        var renderer=part.GetComponent<Renderer>();
        renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
        renderer.receiveShadows=false;
    }
    static void CreateShadowBody(Transform player)
    {
        var body=new GameObject("PlayerShadow").transform;
        body.SetParent(player,false);
        CreateShadowPart(body,"Torso",PrimitiveType.Capsule,new Vector3(0,1.17f,0),new Vector3(.48f,.33f,.25f),Vector3.zero);
        CreateShadowPart(body,"Head",PrimitiveType.Sphere,new Vector3(0,1.68f,0),new Vector3(.25f,.30f,.25f),Vector3.zero);
        CreateShadowPart(body,"LeftLeg",PrimitiveType.Capsule,new Vector3(-.13f,.43f,0),new Vector3(.17f,.42f,.19f),Vector3.zero);
        CreateShadowPart(body,"RightLeg",PrimitiveType.Capsule,new Vector3(.13f,.43f,0),new Vector3(.17f,.42f,.19f),Vector3.zero);
        CreateShadowPart(body,"LeftArm",PrimitiveType.Capsule,new Vector3(-.30f,1.12f,.03f),new Vector3(.13f,.29f,.14f),new Vector3(0,0,-12));
        CreateShadowPart(body,"RightArm",PrimitiveType.Capsule,new Vector3(.30f,1.12f,.03f),new Vector3(.13f,.29f,.14f),new Vector3(0,0,12));
    }
    static void Player(){var p=new GameObject("Player"){tag="Player"};var cc=p.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.center=Vector3.up*.9f;var h=new GameObject("CameraHolder").transform;h.SetParent(p.transform,false);h.localPosition=Vector3.up*1.6f;var co=new GameObject("MainCamera");co.tag="MainCamera";co.transform.SetParent(h,false);var cam=co.AddComponent<Camera>();cam.fieldOfView=75;co.AddComponent<AudioListener>();var hp=Load("Assets/ImportedModels/FPS_Psx_Hands/FPS_Psx_Hands.fbx");FirstPersonHands handAnim=null;if(hp){var hands=Object.Instantiate(hp,h,false);hands.name="Hands";hands.transform.localPosition=new Vector3(0,-.22f,.40f);hands.transform.localRotation=Quaternion.Euler(-65f,0,0);Scale(hands,.52f);handAnim=p.AddComponent<FirstPersonHands>();handAnim.leftHand=FindChild(hands.transform,"forearm.L")??FindChild(hands.transform,"upper_arm.L")??FindChild(hands.transform,"hand.L");handAnim.rightHand=FindChild(hands.transform,"forearm.R")??FindChild(hands.transform,"upper_arm.R")??FindChild(hands.transform,"hand.R");}else Debug.LogError("FPS hands model is missing");CreateShadowBody(p.transform);var f=p.AddComponent<FirstPersonPlayer>();f.head=h;var g=p.AddComponent<MushroomGame>();g.view=cam;g.hands=handAnim;}
    [MenuItem("Mushroom Hunt/Create playable scene")]
    public static void CreateScene(){if(!Application.isBatchMode&&!EditorUtility.DisplayDialog("Create Scene","Replace current scene with imported forest?","Yes","Cancel"))return;Tag("Mushroom");obstaclePositions.Clear();Random.InitState(20261029);var s=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Ground";ground.transform.localScale=Vector3.one*25;ground.GetComponent<Renderer>().sharedMaterial=Pbr();var floor=new GameObject("GroundPhysics");var floorCollider=floor.AddComponent<BoxCollider>();floorCollider.center=new Vector3(0,-1f,0);floorCollider.size=new Vector3(500,2f,500);var forest=new GameObject("ImportedForest").transform;for(int i=0;i<90;i++){float a=i*Mathf.PI*2/90+Random.Range(-.03f,.03f);float r=Random.Range(78,105);Tree(new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),forest);}for(int i=0;i<42;i++){Vector2 q=Random.insideUnitCircle*82;Tree(new Vector3(q.x,0,q.y),forest);}for(int i=0;i<65;i++){Vector2 q=Random.insideUnitCircle*88;Rock(new Vector3(q.x,0,q.y),forest);}for(int i=0;i<70;i++){Vector2 q=Random.insideUnitCircle*85;Detail(new Vector3(q.x,0,q.y),forest);}var ms=new List<Vector3>();int attempts=0;while(ms.Count<18&&attempts++<5000){Vector2 q=Random.insideUnitCircle*68;var v=new Vector3(q.x,0,q.y);bool ok=true;foreach(var x in ms)if(Vector3.Distance(v,x)<7){ok=false;break;}foreach(var x in obstaclePositions)if(Vector3.Distance(v,x)<5){ok=false;break;}if(ok){ms.Add(v);Mushroom(v);}}Player();var sun=new GameObject("Sun");var l=sun.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.2f;l.shadows=LightShadows.Soft;l.shadowStrength=.7f;sun.transform.rotation=Quaternion.Euler(48,-28,0);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.22f,.27f,.22f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.32f,.42f,.34f);RenderSettings.fogStartDistance=70;RenderSettings.fogEndDistance=180;EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s,ScenePath);Debug.Log("Forest rebuilt with upright imported models and stable colliders");}
    [MenuItem("Mushroom Hunt/Build Windows")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds");
        BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/MushroomHunt.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
    }
}






