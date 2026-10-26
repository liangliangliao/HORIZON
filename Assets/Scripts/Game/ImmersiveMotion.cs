using System;

namespace Horizon.Game
{
    // Normalized, seekable curves shared by the board, receipts and movies.
    // These are presentation values: never feed them into resource settlement.
    public static class ImmersiveMotion
    {
        public static float Unit(float value) => Math.Max(0, Math.Min(1, value));
        public static float Smooth(float value) { float p=Unit(value); return p*p*(3-2*p); }
        public static float Flight(float value)
        {
            float p=Unit(value);
            // Let the origin register, accelerate, then land without a hard snap.
            return p<.18f ? .035f*Smooth(p/.18f) : .035f+.965f*Smooth((p-.18f)/.82f);
        }
        public static float Land(float value)
        {
            float p=Unit(value);
            if(p>=1) return 1;
            float q=p-1;
            return 1+2.15f*q*q*q+1.15f*q*q;
        }
        public static float Punch(float age,float duration)
        {
            if(age<0 || age>=duration || duration<=0) return 0;
            float p=age/duration;
            return (float)(Math.Sin(p*Math.PI*2)*Math.Exp(-p*4))*(1-p);
        }
    }
}
