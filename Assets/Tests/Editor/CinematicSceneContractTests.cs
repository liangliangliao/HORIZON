using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Horizon.Game;
using Horizon.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Horizon.Tests
{
    public sealed class CinematicSceneContractTests
    {
        [UnityTest]
        public IEnumerator RigEvaluationPreservesDirectorMovementAndProceduralBodyPose()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Director and character rig contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false }); world.Cinematics.enabled=false;
            world.Cinematics.Enqueue(CinematicEventCoverageTests.RealEvents().Single(e=>e.kind==DomainEventKind.PatternBroken),true);
            var body=world.GetComponentsInChildren<HorizonActorPerformance>().Single(x=>x.name=="Cinematic player");
            var actor=body.GetComponent<HorizonActor>();
            world.Cinematics.Advance(world.Cinematics.Current.Duration*.54f);
            Assert.IsTrue(body.GraphReady);
            Assert.Greater(body.transform.localPosition.z,3.5f,"The character must cross the old break at z=2 before the memento shot.");
            Vector3 crossed=body.transform.localPosition; yield return null;
            Assert.That(Vector3.Distance(crossed,body.transform.localPosition),Is.LessThan(.001f));

            Vector3 position=new Vector3(.4f,.2f,5.2f),scale=new Vector3(.7f,.8f,.9f);
            Quaternion rotation=Quaternion.Euler(0,35,0);
            body.transform.localPosition=position; body.transform.localRotation=rotation; body.transform.localScale=scale;
            body.Play(HorizonBodyAction.Walk,.0625f);
            Quaternion arm=actor.LeftArm.localRotation,leg=actor.LeftLeg.localRotation;
            body.Play(HorizonBodyAction.Walk,.1875f);
            Assert.Greater(Quaternion.Angle(arm,actor.LeftArm.localRotation),40,"Walking must change the actual limb pose after the rig evaluates.");
            Assert.Greater(Quaternion.Angle(leg,actor.LeftLeg.localRotation),40);
            Assert.That(Vector3.Distance(position,body.transform.localPosition),Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(rotation,body.transform.localRotation),Is.LessThan(.01f));
            Assert.That(Vector3.Distance(scale,body.transform.localScale),Is.LessThan(.001f));
            body.Play(HorizonBodyAction.Wait,0); Quaternion forward=actor.Head.localRotation;
            body.Play(HorizonBodyAction.Point,.5f);
            Assert.Greater(Quaternion.Angle(forward,actor.Head.localRotation),1,"The gaze constraint must still solve on top of the supplied pose.");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0,actor.Head.localEulerAngles.y)),2,"Pointing must turn the gaze toward its lateral target.");
            world.Cinematics.Skip(); Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ElevenImaginationScenesKeepTheRealRecoveryChoicesAndPause()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Imagination scene contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false });
            var run=ImaginationEngine.Begin("visual-contract","learn","完成一次学习",1,2);
            var shots=new HashSet<ImaginationShot>();
            for(int step=0;step<20;step++)
            {
                string before=JsonUtility.ToJson(run); world.ShowImaginationScene(run);
                foreach(float time in new[] { 0f,3.1f,6.5f })
                { world.SampleImaginationAt(time); shots.Add(world.CurrentImaginationFrame.Shot); Assert.IsNotEmpty(world.CurrentImaginationFrame.Caption); }
                Assert.AreEqual(before,JsonUtility.ToJson(run));
                var body=world.GetComponentsInChildren<HorizonActorPerformance>().Single(x=>x.name=="Cinematic player");
                Assert.IsTrue(body.GraphReady,"Character animation and gaze rig must be built in the real scene.");
                world.SetPaused(true); Vector3 position=body.transform.position; world.SampleImaginationAt(50); yield return null;
                Assert.AreEqual(position,body.transform.position); world.SetPaused(false);
                if(run.phase==ImaginePhase.Complete) break;
                if(run.phase==ImaginePhase.Recover) ImaginationEngine.Recover(run,RecoveryAction.ChangeMethod);
                else if(run.phase==ImaginePhase.Preparation) ImaginationEngine.Prepare(run,PreparationAction.FixedTime);
                else ImaginationEngine.Continue(run);
            }
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(ImaginationShot)),shots);
            Assert.AreEqual(1,run.recovered); world.EndImaginationScene(); Object.Destroy(world.gameObject);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ResourceDevicesAndSystemScenesReuseWithoutLeakingTransforms()
        {
            yield return new EnterPlayMode(); yield return null;
            var world=new GameObject("Reward scene contract").AddComponent<HorizonWorld3D>(); world.Initialize();
            world.ApplyPreferences(new PlayerPreferences { sound=false,haptics=false,batterySaver=true }); world.Cinematics.enabled=false;
            foreach(int value in new[] { 1,2,5,10,20,100 })
            {
                var e=new DomainEvent { id="resource"+value,kind=DomainEventKind.TimeEcho,tier=RewardTier.Major,
                    receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(value,5,3,9,value,value),sourceDay=1 },day=4 };
                world.Cinematics.Enqueue(e,true); world.Cinematics.Advance(2.7f);
                Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="EnergyCell"));
                Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="Assembled tool 0"));
                Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="Relationship other person"));
                Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="Energy presentation amplifier 0"));
                world.Cinematics.Skip();
            }
            world.Cinematics.Enqueue(new DomainEvent { id="major-energy",kind=DomainEventKind.PatternBroken,tier=RewardTier.Mythic,
                receipt=new RewardReceipt { resourcesRecorded=true,resources=new ResourceDelta(1) } },true);
            world.Cinematics.Advance(7);
            Assert.AreEqual(1,world.Cinematics.Current.Objects.Single(o=>o.Kind==RewardObjectKind.EnergyCell).Amount);
            Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="Energy presentation amplifier 2"));
            Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="Scene power distribution 0"));
            world.Cinematics.Skip();
            var film=new DomainEvent { id="film",kind=DomainEventKind.FutureMemory,tier=RewardTier.Major };
            world.Cinematics.Enqueue(film,true); world.Cinematics.Advance(3); world.Cinematics.Skip();
            world.Cinematics.Enqueue(new DomainEvent { id="next",kind=DomainEventKind.DecisionLocked,tier=RewardTier.Local },true);
            var player=world.GetComponentsInChildren<HorizonActor>().Single(a=>a.name=="Cinematic player");
            Assert.AreEqual(Vector3.one,player.transform.localScale); world.Cinematics.Skip();
            var skill=KnowledgeForge.Learn("forge","遇到困难","缩小一步"); world.ShowForgeScene(skill);
            Assert.IsTrue(world.GetComponentsInChildren<Transform>().Any(t=>t.name=="Knowledge furnace"));
            world.ShowCouncilScene(new[] { new CouncilVoice { name="成长",weight=60 },new CouncilVoice { name="休息",weight=40 } },"成长");
            Assert.AreEqual(2,world.GetComponentsInChildren<HorizonActor>().Count(a=>a.name.StartsWith("Motivation projection")));
            world.ShowReservoirScene(new[] { new InvestmentPool { id="growth",sources=new List<string> { "real:1","real:2" } } });
            Assert.AreEqual(.55f,world.GetComponentsInChildren<Transform>().Single(t=>t.name=="Storage lid").localPosition.y,.001f);
            world.EndImaginationScene();
            var life=new GameSession(2,15);
            ActionRecord immediate=life.Choose(life.Hand[0].Id);
            world.SetTimeline(life.Actions,life.Deadline);
            // Do not capture iterator locals across EnterPlayMode: Unity's
            // domain reload cannot restore a compiler-generated closure here.
            CollectionAssert.Contains(world.GetComponentsInChildren<Transform>().Select(t=>t.name),
                "Pending echo D"+immediate.echoDay+" from D"+immediate.day);
            Object.Destroy(world.gameObject); yield return new ExitPlayMode();
        }
    }
}
