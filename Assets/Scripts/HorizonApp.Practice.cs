using System.Collections;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private RectTransform practicePage, practiceOptions;
        private Text practiceCaption, practiceHeading, practiceMeter;
        private Button practiceNext, practiceBack;
        private bool practiceCut, practiceBackground;

        private void OpenPractice(HorizonCardDrag drag, CardSpec card)
        {
            busy=true;
            var film=PracticeFilms.For(card.Id);
            practicePage=MasterPage("Practice film",card.Name,LeavePractice);
            practicePage.Find("Master shade").GetComponent<Image>().color=Color.clear;
            practicePage.Find("Master back").gameObject.SetActive(false);
            practiceBack=practicePage.Find("Back master").GetComponent<Button>();
            practiceBack.transform.SetAsLastSibling();
            world.BeginPracticeSet(film);
            View.Fill(practicePage,"Subtitle background",new Color(.014f,.03f,.05f,.95f),0,0,1,.33f);
            practiceHeading=View.Label(practicePage,"Scene heading","选择挑战",24,Palette.Mint,TextAnchor.MiddleLeft,.07f,.272f,.94f,.318f);
            practiceCaption=View.Label(practicePage,"Action caption",film.Goal,37,Palette.Text,TextAnchor.MiddleLeft,.07f,.184f,.94f,.27f);
            practiceMeter=View.Label(practicePage,"Reward counter","",54,Palette.Gold,TextAnchor.MiddleCenter,.09f,.69f,.91f,.79f);
            practiceNext=View.Button(practicePage,"Next shot","下一镜",()=>practiceCut=true,.71f,.032f,.94f,.099f,Palette.Panel,Palette.Text,24);
            practiceNext.gameObject.SetActive(false);
            var draft=session.Practice;
            if(draft!=null && draft.cardId==card.Id && draft.day==session.Day && draft.Valid)
            {
                StartCoroutine(RunPractice(practicePage,drag,card,film));
                return;
            }
            NewPracticeOptions();
            for(int i=1;i<=3;i++)
            {
                int difficulty=i;
                float x=.05f+(i-1)*.305f;
                string label=new[]{"轻松","标准","挑战"}[i-1]+" · "+i+" 道困难";
                View.Button(practiceOptions,"Practice difficulty "+i,label,()=>
                {
                    if(practiceOptions==null) return;
                    ClearPracticeOptions(); session.BeginPractice(card,difficulty); PersistMasterAction();
                    StartCoroutine(RunPractice(practicePage,drag,card,film));
                },x,.058f,x+.285f,.153f,Palette.Panel,Palette.Mint,28);
            }
        }

        private void NewPracticeOptions()
        { ClearPracticeOptions(); practiceOptions=View.Rect(practicePage,"Practice options",0,0,1,1); }
        private void ClearPracticeOptions()
        {
            if(practiceOptions==null) return;
            practiceOptions.gameObject.SetActive(false); Destroy(practiceOptions.gameObject); practiceOptions=null;
        }
        private void LeavePractice()
        {
            if(practicePage==null) return;
            practicePage=null; ClearPracticeOptions();
            if(!session.HasChosen) session.AbandonPractice();
            PersistMasterAction(); CloseMasterPage(); BuildBoard();
        }
        private IEnumerator FilmBeat(RectTransform owner,PracticeShot shot,float duration,string heading,string caption,
            PracticeMove move,int beat,int budget=0,int ceiling=0)
        {
            if(owner!=practicePage || owner==null) yield break;
            practiceHeading.text=heading; practiceCaption.text=caption;
            practiceNext.gameObject.SetActive(true); practiceCut=false;
            float elapsed=0;
            while(owner==practicePage && owner!=null)
            {
                if(!practiceBackground && !userPaused)
                {
                    elapsed+=Mathf.Min(.1f,Time.captureFramerate>0?Time.deltaTime:Time.unscaledDeltaTime);
                    float p=practiceCut?1:Mathf.Clamp01(elapsed/duration);
                    world.SamplePractice(shot,p,move,beat);
                    if(shot==PracticeShot.Charge)
                    {
                        // Slow down from a rapid scroll to a short held near-final value.
                        int count=Mathf.FloorToInt((1-Mathf.Pow(1-p,2.15f))*26);
                        uint sample=unchecked((uint)(budget+count*48271)*2654435761u);
                        int amount = p < .045f ? 1 : 1+(int)(sample%(uint)Mathf.Max(1,ceiling));
                        practiceMeter.text="× "+PracticeReward.Format(amount);
                    }
                    if(p>=1) break;
                }
                yield return null;
            }
            if(owner==practicePage && owner!=null) practiceNext.gameObject.SetActive(false);
        }

        private IEnumerator RunPractice(RectTransform owner,HorizonCardDrag drag,CardSpec card,PracticeScript film)
        {
            var take=session.Practice;
            yield return FilmBeat(owner,PracticeShot.Establish,3.1f,"目标",film.Goal,film.MoveA,0);
            if(owner!=practicePage || owner==null) yield break;
            if(take.routes.Length>0)
            {
                int last=take.routes.Length-1; bool b=take.routes[last]=='B';
                yield return FilmBeat(owner,PracticeShot.Route,2.8f,"继续上次的行动",film.Route(last,b),film.Move(last,b),last);
            }
            while(owner==practicePage && owner!=null && !take.Complete)
            {
                int beat=take.routes.Length;
                string obstacle="困难 "+(beat+1)+" / "+take.difficulty;
                yield return FilmBeat(owner,PracticeShot.Setback,3.1f,obstacle,film.Hurdle(beat),film.MoveA,beat);
                if(owner!=practicePage || owner==null) yield break;
                practiceHeading.text=obstacle+"　·　你决定怎么做？";
                NewPracticeOptions();
                bool picked=false, second=false;
                for(int i=0;i<2;i++)
                {
                    bool b=i==1; float y=b?.020f:.102f;
                    View.Button(practiceOptions,"Practice route "+(b?"B":"A"),(b?"B　":"A　")+film.Route(beat,b),
                        ()=>{if(picked) return; picked=true; second=b; session.PickPracticeRoute(b); PersistMasterAction(); ClearPracticeOptions();},
                        .055f,y,.945f,y+.071f,Palette.Panel,b?Palette.Text:Palette.Mint,28);
                }
                while(!picked && owner==practicePage && owner!=null) yield return null;
                if(owner!=practicePage || owner==null) yield break;
                yield return FilmBeat(owner,PracticeShot.Route,3.2f,second?"你的选择 · B":"你的选择 · A",
                    film.Route(beat,second),film.Move(beat,second),beat);
            }
            if(owner!=practicePage || owner==null) yield break;
            int finalBeat=take.difficulty-1;
            yield return FilmBeat(owner,PracticeShot.Success,3.4f,"完成 · "+card.Name,film.Success,film.Move(finalBeat,take.routes[finalBeat]=='B'),finalBeat);
            if(owner!=practicePage || owner==null) yield break;
            int budget=session.PracticeBudget, ceiling=PracticeReward.Ceiling(take.difficulty);
            var actual=session.PreviewPractice(card);
            int[] ticks=PracticeReward.Ticks(actual);
            world.PreparePracticePayout(ticks);
            int first=System.Array.FindIndex(ticks,v=>v>0);
            if(first<0) first=System.Array.FindIndex(ticks,v=>v<0);
            if(first>=0) world.SetPracticePrize(first,ticks[first]<0);
            yield return FilmBeat(owner,PracticeShot.Charge,3.45f,"开奖 · 蓄光","每跨过一道困难，匣上的一枚刻印就会亮起。",film.MoveA,finalBeat,budget,ceiling);
            if(owner!=practicePage || owner==null) yield break;
            practiceMeter.text="× "+PracticeReward.Format(budget);
            yield return FilmBeat(owner,PracticeShot.Open,1.65f,"倍率锁定","最后一枚刻印扣合，奖励匣在刚完成行动的地方展开。",film.MoveA,finalBeat);
            if(owner!=practicePage || owner==null) yield break;
            practiceBack.gameObject.SetActive(false);
            ActionRecord action=CommitPracticeCard(card);
            for(int n=0;n<6;n++)
            {
                int axis=(first+n+6)%6;
                if(ticks[axis]==0) continue;
                world.SetPracticePrize(axis,ticks[axis]<0);
                string change=PracticeReward.Names[axis]+" "+(ticks[axis]>0?"+":"")+PracticeReward.Format(ticks[axis]);
                practiceMeter.text=change;
                yield return FilmBeat(owner,PracticeShot.Receive,2.5f,"本次实际变化",RewardAction(axis,ticks[axis]>0),film.MoveA,finalBeat);
                if(owner!=practicePage || owner==null) yield break;
            }
            practiceHeading.text="已结算　·　× "+PracticeReward.Format(budget);
            practiceCaption.text=PracticeReward.Label(action.actualNow);
            practiceMeter.text="";
            NewPracticeOptions();
            View.Button(practiceOptions,"Finish practice","收下结果",()=>
            {
                ClearPracticeOptions(); practicePage=null; CloseMasterPage();
                StartCoroutine(ResolveChoice(drag,card,action));
            },.16f,.04f,.84f,.14f,Palette.Mint,Palette.Ink,32);
        }

        private static string RewardAction(int axis,bool gain)
        {
            string[] up={"鞋落到脚边，人物重新直起身。","花从匣中升起，肩膀慢慢放松。","书页展开，手指重新找到要读的那一行。",
                "一枚金币落入手边的储备格。","一只杯子递向桌边，为另一个人留了位置。","双手对齐模型的接点，再一次稳稳装好。"};
            string[] down={"鞋落回地面，人物停下来歇脚。","花枝低垂，人物停下来缓一口气。","视线离开书页，需要重新找回注意力。",
                "金币从手边滑入支出格，这次花费留下记录。","杯子被移远，这次相处留下了一点距离。","模型倾斜，人物收手查看仍待练习的动作。"};
            return (gain?up:down)[axis];
        }
    }
}
