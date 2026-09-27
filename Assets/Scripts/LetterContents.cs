using UnityEngine;
public static class LetterContents
{
    public static string Body(string id)
    {
        switch(id)
        {
            case "Letter1": return L.T("가끔은 땅 속으로 숨고 싶을 때가 있죠..\n그래서 저는 이 구멍 안으로 들어갔답니다.\n혹시 따라오고 싶으면 따라오셔도 괜찮습니다.\n사실은 따라오길 기대하고 있어요..","Sometimes you just want to hide underground..\nSo I slipped into this hole.\nYou're welcome to follow me if you'd like.\nTo tell the truth, I'm hoping you will..");
            case "Letter2": return L.T("가끔은 하늘을 날 듯이 기쁠 때가 있죠..\n그래서 저는 하늘로 날아갔답니다.\n혹시 따라오고 싶으면 따라오셔도 괜찮습니다.\n사실은 따라오길 기대하고 있어요..","Sometimes you feel so happy you could fly..\nSo I flew up into the sky.\nYou're welcome to follow me if you'd like.\nTo tell the truth, I'm hoping you will..");
            case "Letter3": return L.T("말도 안 되게 멀리\n떠나고 싶은 날엔\n별들 사이로 가렴..\n\n은하수가 길을\n비춰줄 거야.","On days when you want to go\nfarther than you can imagine,\nfind your way among the stars..\n\nThe Milky Way\nwill light your path.");
            case "Letter4": return L.T("숨이 차게 달리고\n싶은 날엔\n트랙을 달리렴.\n\n결승선은 언제나\n너를 기다린단다.","On days when you want to run\nuntil you're out of breath,\nhead for the track.\n\nThe finish line\nis always waiting for you.");
            case "Letter5": return L.T("높이 뛰어오르고\n싶은 날엔\n달로 가렴.\n\n그곳에선 모두가\n가볍게 난단다.","On days when you want\nto leap up high,\ncome to the moon.\n\nEveryone floats\na little lighter here.");
            case "Letter6": return L.T("겁이 나서 발이 안 떨어지는\n날엔 이 협곡에 와 보렴.\n\n높은 절벽 앞에선 다리를\n길게 늘이고,\n\n미끄러운 얼음 위에선\n그대로 미끄러지고,\n\n바닥이 없는 골짜기는\n훨훨 날아 건너면 된단다.\n\n한 걸음씩 가다 보면\n못 가는 곳이 없어.","On days when fear keeps your feet still,\ncome to this canyon.\n\nStretch your legs\nto climb the tall cliffs.\n\nGlide across\nthe slippery ice.\n\nFly over the valleys\nwith no ground below.\n\nTake one step at a time,\nand there's nowhere you can't go.");
            default: return "";
        }
    }
}
