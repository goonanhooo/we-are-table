using UnityEngine;

/// <summary>Story characters use the same empty cloud; dialogue is always live text.</summary>
public static class LocalizedStoryBubble
{
    public static void Draw(Rect sourceRect,bool second,bool caterpillar=false,Vector2? speaker=null,float radius=45f)
    {
        string words=second?L.T("내 힘을 줄게...","You can have my power..."):caterpillar?L.T("잘했어...","Well done..."):L.T("날 도와줘서 고마워..","Thank you for helping me..");
        var actual=speaker??new Vector2(sourceRect.xMax+60,sourceRect.center.y+60);
        Say(words,actual,sourceRect.center.x<actual.x,radius);
    }
    /// <summary>아무 대사나 같은 구름·글씨로(우주오징어 등). speaker = 화면 좌표(GUI, 위가 0), preferLeft = 말하는 쪽 왼편에 구름.</summary>
    public static void Say(string words,Vector2 actual,bool preferLeft,float radius=45f)
    {
        float scale=ThoughtBubbleUI.Scale,origin=(Screen.width-ThoughtBubbleUI.VW*scale)*.5f;
        var target=new Vector2((actual.x-origin)/scale,actual.y/scale);
        float safeRadius=Mathf.Clamp(radius/scale,32,100);
        var avoid=new Rect(target.x-safeRadius,target.y-safeRadius,safeRadius*2,safeRadius*2);
        var body=PowerBubbleLayout.Place(avoid,avoid,540,preferLeft);
        ThoughtBubbleUI.Begin();
        ThoughtBubbleUI.DrawPointingTail(body,target,1);
        ThoughtBubbleUI.DrawWideBubble(body,1,words,30);
        ThoughtBubbleUI.End();
    }
}
