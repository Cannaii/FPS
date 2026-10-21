using System;
using AFPS.Math;
using AFPS.Core.Collections;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Prediction;
using AFPS.Simulation.Characters;
using AFPS.Simulation.Characters.Collision;
using AFPS.Simulation.World;

internal static class CapsuleWorldReplay
{
    private static Fix F(int x) => Fix.FromInt(x);
    private static FixVec3 V(int x,int y,int z) => new FixVec3(F(x),F(y),F(z));
    private static int Main()
    {
        var world=new FixedWorld(new[] {
            new FixedWorldBox(10,0,FixedSurface.Solid|FixedSurface.Ground,new FixedAabb(V(1,0,-2),new FixVec3(F(3),Fix.FromRatio(1,5),F(2)))),
            new FixedWorldBox(11,0,FixedSurface.Solid,new FixedAabb(V(-3,0,-1),V(-2,3,1))) },new[] {
            new FixedWorldPlane(1,0,FixedSurface.Solid,FixedAxis.Y,Fix.Zero),
            new FixedWorldPlane(2,0,FixedSurface.Solid,FixedAxis.Y,F(4),false),
            new FixedWorldPlane(3,0,FixedSurface.Solid,FixedAxis.X,F(-8)),
            new FixedWorldPlane(4,0,FixedSurface.Solid,FixedAxis.X,F(8),false),
            new FixedWorldPlane(5,0,FixedSurface.Solid,FixedAxis.Z,F(-8)),
            new FixedWorldPlane(6,0,FixedSurface.Solid,FixedAxis.Z,F(8),false) });
        if(!FixedWorldCodec.TryDeserialize(new ArraySegment<byte>(world.CreateSnapshot()),out var restored,out _)) throw new Exception("World snapshot failed");
        var backend=new FixedCharacterCollisionWorld(restored);
        var config=new PlayerSimulationConfig(F(6),F(20),F(20),F(8));
        var random=new FixRandom(20261025);
        ulong hash=14695981039346656037UL;
        for(int i=0;i<4096;i++)
        {
            var feet=new FixVec3(Fix.FromRaw(random.NextInt(-10*65536,10*65536)),Fix.FromRaw(random.NextInt(-2*65536,6*65536)),Fix.FromRaw(random.NextInt(-10*65536,10*65536)));
            var direction=new FixVec3(Fix.FromRaw(random.NextInt(-65536,65537)),Fix.FromRaw(random.NextInt(-65536,65537)),Fix.FromRaw(random.NextInt(-65536,65537)));
            var hit=restored.CapsuleCast(feet,Fix.Half,F(2),direction,F(20));
            Add(ref hash,unchecked((int)hit.ShapeId)); Add(ref hash,hit.Distance.Raw);
            Add(ref hash,hit.Normal.X.Raw); Add(ref hash,hit.Normal.Y.Raw); Add(ref hash,hit.Normal.Z.Raw);
            Add(ref hash,hit.StartedInside ? 1:0);
        }
        var state=new PlayerState {Tick=uint.MaxValue-2048,IsGrounded=true};
        var inputs=new TickBuffer<PlayerInputCommand>(128); var states=new TickBuffer<PlayerState>(128);
        var packet=new byte[AuthoritativePlayerStateCodec.PacketSize];
        int contacts=0;
        for(int i=0;i<8192;i++)
        {
            var input=new PlayerInputCommand {Tick=unchecked(state.Tick+1),JumpPressed=i%71==0};
            // 连续一段移动方向，确保实际抵达墙、台阶、天花板，而非随机输入在原地抖动。
            input.TrySetEncodedControls((sbyte)(i%400<200 ? 127:-127),127,(ushort)((i/400*3700)%36000),0);
            inputs.Store(input.Tick,input);
            state=PlayerSimulation.Simulate(state,input,config,SimulationStep.Step50Hz,backend);
            states.Store(state.Tick,state);
            if(state.VelocityFixed.Y==Fix.Zero || state.VelocityFixed.X==Fix.Zero || state.VelocityFixed.Z==Fix.Zero) contacts++;
            if(!AuthoritativePlayerStateCodec.TrySerialize(new AuthoritativePlayerState((uint)i,state.Tick,state),(uint)i,new ArraySegment<byte>(packet),out _)) throw new Exception("Serialize failed");
            if(!AuthoritativePlayerStateCodec.TryDeserialize(new ArraySegment<byte>(packet),out _,out var authority) || !state.RawEquals(authority.State)) throw new Exception("Raw packet mismatch");
            if(i>8 && i%43==0)
            {
                uint ack=unchecked(state.Tick-7); states.TryGet(ack,out var anchor);
                var corrupt=anchor; corrupt.PositionFixed+=new FixVec3(Fix.Epsilon,Fix.Zero,Fix.Zero); states.Store(ack,corrupt);
                var result=ClientPredictionReconciler.Reconcile(new AuthoritativePlayerState((uint)i,ack,anchor),state.Tick,state,inputs,states,config,SimulationStep.Step50Hz,backend);
                if(result.Status!=ReconciliationStatus.Corrected || !result.State.RawEquals(state)) throw new Exception("Geometry rollback mismatch");
            }
            ulong raw=PlayerStateHash.Compute(state);
            Add(ref hash,unchecked((int)raw)); Add(ref hash,unchecked((int)(raw>>32)));
        }
        if(contacts<100) throw new Exception("Fixture did not exercise sufficient collisions");
        Console.WriteLine($"CapsuleWorldReplay={hash}; Sweeps=4096; Ticks=8192; Contacts={contacts}; World={restored.VersionHash}");
        if(hash != 17683167043001353772UL) throw new Exception("Capsule world replay fingerprint changed");
        return 0;
    }
    private static void Add(ref ulong hash,int value)
    {
        uint bits=unchecked((uint)value);
        for(int shift=0;shift<32;shift+=8) hash=unchecked((hash^((bits>>shift)&255u))*1099511628211UL);
    }
}
