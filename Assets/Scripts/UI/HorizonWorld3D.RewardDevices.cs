using Horizon.Game;
using UnityEngine;

namespace Horizon.UI
{
    public sealed partial class HorizonWorld3D
    {
        private void BuildEnergyDevice(Transform item, RewardObjectForm form)
        {
            bool storage=form==RewardObjectForm.StorageUnit || form==RewardObjectForm.EnergyCore;
            int count=form==RewardObjectForm.BatteryPack?6:storage?4:1;
            if(count>1) Box(item,"Battery carrier",new Vector3(0,-.37f,0),new Vector3(.85f,.1f,.58f),ivory);
            for(int i=0;i<count;i++)
            {
                Vector3 at=count==1?Vector3.zero:new Vector3((i%3-1)*.24f,0,(i/3-.5f)*.28f);
                Shape(item,"Energy cell "+i,PrimitiveType.Cylinder,at,new Vector3(.2f,.32f,.2f),teal);
                Shape(item,"Cell terminal "+i,PrimitiveType.Cylinder,at+Vector3.up*.34f,new Vector3(.08f,.035f,.08f),gold);
                Box(item,"Cell charge "+i,at+new Vector3(0,0,-.105f),new Vector3(.08f,.36f,.025f),portalLight);
            }
            if(storage)
            {
                Box(item,"Storage enclosure",new Vector3(0,-.05f,.18f),new Vector3(.95f,.9f,.12f),dark);
                Ring(item,"Storage induction ring",new Vector3(0,.55f,0),.48f,portalLight,false);
            }
            if(form==RewardObjectForm.EnergyCore)
            {
                Shape(item,"Energy core",PrimitiveType.Sphere,new Vector3(0,.65f,0),Vector3.one*.48f,warmLight);
                Ring(item,"Core rotor",new Vector3(0,.65f,0),.65f,gold,true);
            }
        }
        private void BuildMoneyDevice(Transform item, RewardObjectForm form)
        {
            Transform coins=Group("Money arriving",item);
            int count=form==RewardObjectForm.Coin?1:form==RewardObjectForm.Wallet?3:5;
            for(int i=0;i<count;i++)
            {
                if(form==RewardObjectForm.CashBundle || form==RewardObjectForm.Vault)
                    Box(coins,"Cash note "+i,new Vector3(0,i*.04f,0),new Vector3(.42f,.025f,.21f),ivory);
                else Shape(coins,"Coin "+i,PrimitiveType.Cylinder,new Vector3(i*.09f,0,0),new Vector3(.23f,.025f,.23f),gold);
            }
            if(form==RewardObjectForm.Vault)
            {
                Box(item,"Vault body",new Vector3(0,-.12f,.19f),new Vector3(.85f,.85f,.3f),dark);
                Transform hinge=Group("Money opening",item); hinge.localPosition=new Vector3(-.43f,-.12f,0);
                Box(hinge,"Vault door",new Vector3(.43f,0,0),new Vector3(.85f,.83f,.09f),ivory);
                Ring(hinge,"Vault wheel",new Vector3(.43f,0,-.08f),.17f,gold,true);
            }
            else
            {
                Box(item,"Wallet receiving pocket",new Vector3(0,-.28f,.1f),new Vector3(.65f,.36f,.24f),wood);
                Transform hinge=Group("Money opening",item); hinge.localPosition=new Vector3(-.32f,-.1f,0);
                Box(hinge,"Wallet flap",new Vector3(.32f,0,0),new Vector3(.65f,.3f,.035f),dark);
            }
        }
        private void BuildToolDevice(Transform item, RewardObjectForm form)
        {
            bool box=form==RewardObjectForm.ToolBox || form==RewardObjectForm.Default;
            if(box)
            {
                Box(item,"Tool case",new Vector3(0,-.3f,.08f),new Vector3(.84f,.35f,.48f),ivory);
                Box(item,"Toolbox handle",new Vector3(0,-.05f,.13f),new Vector3(.34f,.06f,.08f),gold);
            }
            int count=box?3:1;
            for(int i=0;i<count;i++)
            {
                Transform tool=Group("Assembled tool "+i,item); tool.localPosition=new Vector3((i-(count-1)*.5f)*.25f,.15f,0);
                Box(tool,"Tool shaft",Vector3.zero,new Vector3(.065f,.43f,.065f),wood);
                Box(tool,"Tool head",new Vector3(0,.22f,0),new Vector3(.23f,.13f,.12f),ivory);
                Box(tool,"Tool joint",new Vector3(0,.15f,0),new Vector3(.10f,.07f,.09f),gold);
            }
        }
        private void BuildTriggerDevice(Transform item, TriggerAppearance appearance)
        {
            if(appearance==TriggerAppearance.Key)
            {
                Sculpt(item,"Key bow",rewardRing,new Vector3(-.18f,0,0),Vector3.one*.45f,gold);
                Box(item,"Key shaft",new Vector3(.2f,0,0),new Vector3(.58f,.09f,.09f),ivory);
                for(int i=0;i<2;i++) Box(item,"Key tooth",new Vector3(.32f+i*.12f,-.09f,0),new Vector3(.07f,.2f,.09f),ivory);
            }
            else if(appearance==TriggerAppearance.Alarm || appearance==TriggerAppearance.Countdown)
            {
                Shape(item,"Clock face",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.6f,.08f,.6f),ivory).localRotation=Quaternion.Euler(90,0,0);
                Box(item,"Minute hand",new Vector3(0,.08f,-.1f),new Vector3(.035f,.23f,.03f),dark);
                Box(item,"Hour hand",new Vector3(.08f,0,-.1f),new Vector3(.18f,.035f,.03f),dark);
                for(int i=-1;i<=1;i+=2) Shape(item,"Alarm bell",PrimitiveType.Sphere,new Vector3(i*.24f,.28f,0),Vector3.one*.19f,gold);
            }
            else
            {
                Box(item,appearance==TriggerAppearance.Ticket?"Travel ticket":"Appointment credential",Vector3.zero,new Vector3(.68f,.43f,.055f),ivory);
                Box(item,"Credential heading",new Vector3(0,.12f,-.035f),new Vector3(.58f,.09f,.025f),teal);
                for(int i=0;i<4;i++) Box(item,"Printed appointment line",new Vector3(-.08f,-.04f-i*.055f,-.035f),new Vector3(.36f,.018f,.02f),dark);
                if(appearance==TriggerAppearance.Reminder || appearance==TriggerAppearance.Commitment)
                    Sculpt(item,"Support seal",rewardRing,new Vector3(.22f,-.08f,-.05f),Vector3.one*.18f,gold);
            }
        }
        private void SampleRewardDevice(Transform item, MaterialReward reward, float arrival)
        {
            if(reward.Kind==RewardObjectKind.HorizonLens)
            {
                Transform body=item.Find("Telescope body"), finder=item.Find("Viewfinder");
                body.localScale=new Vector3(.43f,.43f,Mathf.Lerp(.18f,.7f,arrival));
                body.localPosition=new Vector3(0,0,Mathf.Lerp(.05f,.28f,arrival));
                finder.localPosition=new Vector3(0,Mathf.Lerp(.12f,.35f,arrival),.35f);
            }
            if(reward.Kind==RewardObjectKind.WarmLamp)
                SetMovieColor(item.Find("Warm globe"),Color.Lerp(Palette.Muted*.25f,new Color(1,.7f,.35f),reward.Amount>0?arrival:1-arrival));
            if(reward.Kind==RewardObjectKind.ToolKit)
                foreach(Transform part in item.GetComponentsInChildren<Transform>())
                    if(rewardRestPositions.TryGetValue(part,out Vector3 rest))
                    {
                        if(part.name=="Tool head") part.localPosition=rest+new Vector3(.35f,.4f,0)*(1-arrival);
                        if(part.name=="Tool joint") part.localPosition=rest+new Vector3(-.4f,.15f,0)*(1-arrival);
                        if(part.name.StartsWith("Assembled tool")) part.localPosition=rest+Vector3.up*(1-arrival)*.6f;
                    }
            if(reward.Kind==RewardObjectKind.MoneyWallet)
            {
                Transform hinge=item.Find("Money opening"),money=item.Find("Money arriving");
                if(hinge!=null) hinge.localRotation=Quaternion.Euler(0,-Mathf.Sin(arrival*Mathf.PI)*100,0);
                if(money!=null) money.localPosition=Vector3.Lerp(new Vector3(.45f,.8f,-.3f),new Vector3(0,-.2f,.02f),arrival);
            }
            if(reward.Kind==RewardObjectKind.EnergyCell)
            {
                Transform rotor=item.Find("Core rotor");
                if(rotor!=null) rotor.localRotation=Quaternion.Euler(90,0,arrival*180);
            }
            if(reward.Kind==RewardObjectKind.RouteLock)
            { Transform shackle=item.Find("Lock shackle"); shackle.localPosition=new Vector3(0,.08f+(1-arrival)*.45f,0); }
            if(reward.Kind==RewardObjectKind.PredictionPanel)
            {
                RewardReceipt r=moviePlan.Event.receipt;
                if(r==null || !r.predictionRecorded) return;
                var predicted=item.Find("Predicted trajectory").GetComponent<LineRenderer>();
                var actual=item.Find("Actual trajectory").GetComponent<LineRenderer>();
                int[] expected=ResourceMath.Axes(r.predicted),observed=ResourceMath.Axes(r.actual);
                predicted.positionCount=actual.positionCount=6;
                actual.gameObject.SetActive(r.predictionResolved);
                for(int i=0;i<6;i++)
                {
                    float x=-.34f+i*.136f;
                    predicted.SetPosition(i,new Vector3(x,Mathf.Clamp(expected[i],-10,10)*.021f,-.04f));
                    actual.SetPosition(i,new Vector3(x,Mathf.Clamp(observed[i],-10,10)*.021f,-.065f));
                }
            }
        }
    }
}
