using UnityEngine;

public static class CheckpointCatalog
{
    public sealed class Point
    {
        public readonly int id, preview; public readonly string scene, ko, en;
        public Point(int id,string scene,int preview,string ko,string en){this.id=id;this.scene=scene;this.preview=preview;this.ko=ko;this.en=en;}
        public string Name => L.T(ko,en);
        public bool SceneStart => id==0||id==1||id==5||id==13;
    }
    public static readonly Point[] All={
        new Point(0,"Hallway",0,"복도에서 시작","Hallway start"),
        new Point(1,"Jungle",1,"정글에 도착","Jungle arrival"),
        new Point(2,"Jungle",1,"기린의 힘 획득","Giraffe power acquired"),
        new Point(3,"Jungle",1,"용암을 건넌 뒤","Across the lava"),
        new Point(4,"Jungle",1,"스위치 앞","At the switches"),
        new Point(5,"IceCavern",2,"동굴에 도착","Cave arrival"),
        new Point(6,"IceCavern",2,"테니스공의 힘 획득","Tennis power acquired"),
        new Point(7,"IceCavern",2,"첫 광차","The first minecart"),
        new Point(8,"IceCavern",3,"눈사람 동굴에 도착","Snowman's cave"),
        new Point(9,"IceCavern",3,"눈사람의 힘 획득","Snowman power acquired"),
        new Point(10,"IceCavern",3,"먼 플랫폼에 도착","The distant platform"),
        new Point(11,"Space",4,"우주오징어의 힘 획득","Space squid power acquired"),
        new Point(12,"Space",4,"달에 접근","Approaching the moon"),
        new Point(13,"Moon",5,"달에서 시작","Moon arrival"),
        new Point(14,"Moon",5,"집에 도착","Home at last"),
        new Point(15,"Moon",5,"엔딩 다시 보기","Replay the ending")
    };
    public static bool Valid(int id)=>id>=0&&id<All.Length;
    public static bool Matches(int id,string scene)=>Valid(id)&&(All[id].scene==scene||(id>=13&&scene=="Moon_CozyKitchen"));
    public static int SceneEntry(string scene)=>scene=="Hallway"?0:scene=="Jungle"?1:scene=="IceCavern"?5:scene=="Moon"||scene=="Moon_CozyKitchen"?13:-1;
    public static string Label(int id,uint mask)=>Valid(id)&&(mask&(1u<<id))!=0?All[id].Name:"???";
}
