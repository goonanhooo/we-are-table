using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class KeyTutorial
{
    readonly Transform[] practiceLegs = new Transform[4];
    readonly float[] practiceBaseLengths = new float[4];
    static string P(string ko,string en,string ja,string zh) => L.T(ko,en,ja,zh);
    static Key PowerKey(string power) => power=="giraffe"?Key.Digit2:power=="tennis"?Key.Digit3:power=="ice"?Key.Digit4:Key.Digit5;
    bool PowerOn(string power) => power=="giraffe"?giraffe!=null&&giraffe.active:power=="tennis"?tennis!=null&&tennis.active:power=="ice"?ice!=null&&ice.active:squid!=null&&squid.LookOn;
    bool PracticeEnabled(string power) => power=="giraffe"?showGiraffe:power=="tennis"?showTennis:power=="ice"?showIce:showSquid;
    bool OwnAnyLeg() { for(int i=0;i<4;i++)if(legMine[i])return true;return false; }
    bool KeyHeld(Key key)
    {
        if (Keyboard.current!=null && Keyboard.current[key].isPressed) return true;
        int power = key==Key.Digit2?2:key==Key.Digit3?3:key==Key.Digit4?4:key==Key.Digit5?5:0;
        return power!=0 && PhoneControllerBridge.PowerHeld(power);
    }
    LegInput PracticeInput(int leg) => ti!=null?ti.Get((LegControlSet)leg):default;
    float PracticeLength(int leg) => practiceLegs[leg]!=null?practiceLegs[leg].localScale.y:0f;
    string PowerName(string p) => p=="giraffe"?P("기린의 힘","Giraffe power","キリンの力","长颈鹿之力"):p=="tennis"?P("테니스공의 힘","Tennis power","テニスボールの力","网球之力"):p=="ice"?P("눈사람의 힘","Snowman power","雪だるまの力","雪人之力"):P("우주오징어의 힘","Space squid power","宇宙イカの力","宇宙乌贼之力");
    bool EarlierPracticeDone(Lesson current)
    {
        foreach(var l in lessons)
        {
            if(l==current)break;
            if(l.power!=current.power || l.finished || (l.ownerLeg>=0&&!legMine[l.ownerLeg]))continue;
            return false;
        }
        return true;
    }
    Lesson Practice(string id,string power,int leg,Key[] keys,Func<int,bool> held,Func<int,bool> effect,Func<string> heading,Func<int,string> caption,Action<int> begin,params float[] durations)
    {
        Lesson l=null;
        l=AddLesson(id,()=>PracticeEnabled(power)&&Got(power)&&legsCaptured&&OwnAnyLeg()
            &&(leg<0||legMine[leg])&&EarlierPracticeDone(l),null,heading,null);
        l.power=power;l.ownerLeg=leg;l.practice=new PowerPracticeProgress(durations);l.held=held;l.effect=effect;l.beginAction=begin;
        l.caption=()=>
        {
            if(l.practice.Satisfied)return P("키에서 손을 떼면 다음 단계로","Release the key to continue","キーを離すと次へ","松开按键继续");
            if(!PowerOn(power)&&!id.StartsWith("activate")&&!id.StartsWith("enable"))return "[ "+LabelOf(PowerKey(power))+" ] "+P("힘을 켜고 연습해 보자","Turn the power on to practice","力をオンにして練習しよう","开启能力后练习");
            return caption(l.practice.Stage);
        };
        l.onStart=()=>begin?.Invoke(0);
        l.fill=()=>
        {
            for(int n=0;n<keys.Length;n++)
            {
                Add(keys[n],id,leg>=0?LegPalette.BySlot[leg]:Color.white,leg>=0);
                foreach(var c in caps)if(c.group==id&&c.key==keys[n])c.practiceAction=keys.Length==durations.Length?n:-1;
            }
            if(!id.StartsWith("activate")&&!id.StartsWith("enable"))Add(PowerKey(power),id,Color.white,false,true);
        };
        return l;
    }
    void ActivationPractice(string power)
    {
        Key key=PowerKey(power);bool initial=false;
        Practice("activate_"+power,power,-1,new[]{key},_=>KeyHeld(key),n=>PowerOn(power)==(n==0?!initial:initial),
            ()=>"[ "+LabelOf(key)+" ]  "+PowerName(power),
            n=>P("직접 켜고 꺼 보자","Try switching on and off","オン・オフを試そう","试着开启和关闭")+"  "+(n+1)+" / 2",
            n=>{if(n==0)initial=PowerOn(power);},0f,0f);
        var enable=Practice("enable_"+power,power,-1,new[]{key},_=>KeyHeld(key),_=>PowerOn(power),()=>PowerName(power),
            _=>P("힘을 켜고 시작하자","Turn the power on","力をオンにしよう","开启能力"),null,0f);
        // Two toggles return to the initial state. If initially off, finish with the power on.
        enable.skipIf=()=>PowerOn(power);
    }
    void BuildPowerLessons()
    {
        for(int i=0;i<4;i++){practiceLegs[i]=LegLayout.Find(transform.root,i);practiceBaseLengths[i]=PracticeLength(i);}
        ActivationPractice("giraffe");
        for(int slot=0;slot<4;slot++)
        {
            int i=slot;float initial=0;
            Practice("stretch"+i,"giraffe",i,new[]{Map[i][4],Map[i][5]},n=>n==0?PracticeInput(i).longer:PracticeInput(i).shorter,
                n=>PowerOn("giraffe")&&(n==0?PracticeLength(i)>=initial+.06f||PracticeLength(i)>=practiceBaseLengths[i]*giraffe.maxMultiplier-.015f:PracticeLength(i)<=initial-.06f||PracticeLength(i)<=practiceBaseLengths[i]+.015f),
                ()=>PowerName("giraffe")+" · "+LegLayout.Name(i),
                n=>"[ "+LabelOf(Map[i][n==0?4:5])+" ] "+(n==0?P("길게 늘려 보자","Make this leg longer","この脚を伸ばそう","伸长这条腿"):P("짧게 줄여 보자","Make this leg shorter","この脚を縮めよう","缩短这条腿")),
                _=>initial=PracticeLength(i),.12f,.12f);
        }
        var initialLengths=new float[4];
        Practice("stretchAll","giraffe",-1,new[]{Key.Equals,Key.Minus},n=>giraffe!=null&&(n==0?giraffe.AllLongerHeld:giraffe.AllShorterHeld),n=>
        {
            if(!PowerOn("giraffe"))return false;
            for(int i=0;i<4;i++)if(n==0?PracticeLength(i)<initialLengths[i]+.06f&&PracticeLength(i)<practiceBaseLengths[i]*giraffe.maxMultiplier-.015f:PracticeLength(i)>initialLengths[i]-.06f&&PracticeLength(i)>practiceBaseLengths[i]+.015f)return false;
            return true;
        },()=>P("+ / −  네 다리를 한 번에!","+ / −  All four legs at once!","+ / −  4本の脚を一度に！","+ / −  同时控制四条腿！"),
        n=>n==0?P("+ (= 키)를 길게 눌러 모두 늘려 보자","Hold + (the = key) to extend all legs","+（=キー）を長押しして全部伸ばそう","按住 +（= 键）伸长所有腿"):P("− 키를 길게 눌러 모두 줄여 보자","Hold − to shorten all legs","−を長押しして全部縮めよう","按住 − 缩短所有腿"),
        _=>{for(int i=0;i<4;i++)initialLengths[i]=PracticeLength(i);},.18f,.18f);

        ActivationPractice("tennis");
        for(int slot=0;slot<4;slot++)
        {
            int i=slot;
            Practice("tennisPractice"+i,"tennis",i,new[]{GrabKeys[i],ReleaseKeys[i]},n=>n==0?PracticeInput(i).grab:PracticeInput(i).release,
                n=>PowerOn("tennis")&&tennis.PracticeStuck((LegControlSet)i)==(n==0),
                ()=>PowerName("tennis")+" · "+LegLayout.Name(i),
                n=>"[ "+LabelOf(n==0?GrabKeys[i]:ReleaseKeys[i])+" ] "+(n==0?P("바닥이나 물체에 발을 대고 붙여 보자","Touch the floor or an object, then stick","床や物に足を当ててくっつけよう","让脚接触地面或物体，再粘住"):P("붙인 발을 떼어 보자","Detach this foot","くっつけた足を離そう","松开粘住的脚")),null,0f,0f);
        }
        ActivationPractice("ice");
        Vector3 slideStart=Vector3.zero;int slideLeg=0;
        var slide=Practice("iceSlide","ice",-1,new Key[0],_=>
        {var v=PracticeInput(slideLeg);return v.up||v.down||v.left||v.right;},
        _=>PowerOn("ice")&&root!=null&&Vector2.Distance(new Vector2(root.transform.position.x,root.transform.position.z),new Vector2(slideStart.x,slideStart.z))>.15f,
        ()=>PowerName("ice"),_=>P("방향키로 밀어서 미끄러져 보자 · 4로 해제","Push with the movement keys to slide · 4 to turn off","移動キーで押して滑ろう · 4で解除","用移动键推动滑行 · 4 关闭"),
        _=>{slideStart=root!=null?root.transform.position:Vector3.zero;for(int i=0;i<4;i++)if(legMine[i]){slideLeg=i;break;}},.12f);
        slide.fill=()=>{var keys=slideLeg==3?new[]{Key.LeftBracket,Key.Quote,Key.Semicolon,Key.Enter}:new[]{Map[slideLeg][0],Map[slideLeg][1],Map[slideLeg][2],Map[slideLeg][3]};foreach(var key in keys)Add(key,slide.id,LegPalette.BySlot[slideLeg],true);Add(Key.Digit4,slide.id,Color.white,false,true);};

        ActivationPractice("squid");
        for(int slot=0;slot<4;slot++)
        {
            int i=slot;
            var jet=Practice("squidPractice"+i,"squid",i,new[]{Map[i][4]},_=>PracticeInput(i).longer,
                _=>PowerOn("squid")&&squid.PracticeJetting((LegControlSet)i),
                ()=>PowerName("squid")+" · "+LegLayout.Name(i),
                _=>P("길게 눌러 분사하고 손을 떼어 멈춰 보자","Hold to thrust, then release to stop","長押しで噴射し、離して止めよう","按住喷射，松开停止"),null,.2f);
            jet.releaseEffect=_=>!squid.PracticeJetting((LegControlSet)i);
        }
    }
}
