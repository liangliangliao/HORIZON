#ifndef HORIZON_ENERGY_RELEASE
#define HORIZON_ENERGY_RELEASE

float energyHash(float2 p)
{
    p=frac(p*float2(.1031,.11369));
    p+=dot(p,p.yx+19.19);
    return frac((p.x+p.y)*p.x);
}

float energyNoise(float2 p)
{
    float2 cell=floor(p), f=frac(p);
    f=f*f*(3-2*f);
    return lerp(lerp(energyHash(cell),energyHash(cell+float2(1,0)),f.x),
                lerp(energyHash(cell+float2(0,1)),energyHash(cell+1),f.x),f.y);
}

// Use polar lanes as an analytic emitter. Each lane has its own birth, speed,
// length and lifetime. Depth comes from separate world-space instances.
float energyStreams(float2 p,float clock,float seed,float gain,float laneCount)
{
    float turn=(atan2(p.y,p.x)+3.14159265)/6.2831853;
    float lane=floor(turn*laneCount);
    float random=energyHash(float2(lane,seed));
    float lifetime=.34+random*.84;
    float cycle=clock/lifetime+random*8.4;
    float birth=energyHash(float2(lane,floor(cycle)+seed*31));
    float age=frac(cycle);
    float radius=length(p);
    float head=lerp(.98,.18,age);
    float distance=radius-head;
    float laneWidth=abs(frac(turn*laneCount)-lerp(.24,.76,birth));
    float filament=exp2(-laneWidth*laneWidth*lerp(22,220,birth));
    float envelope=smoothstep(0,.18,age)*(1-smoothstep(.70,1,age));
    float tail=exp2(-abs(distance)*(distance<0?119:lerp(10,41,birth)));
    return filament*tail*envelope*smoothstep(random*.66,random*.66+.16,gain);
}

float3 releaseLight(float2 p,float age,float flash,float fire,float wave,float3 tint,float seed)
{
    float r=length(p);
    float3 energy=0;
    if(flash>.001)
    {
        float white=exp2(-r*r*24)*flash;
        energy+=float3(1.0,.98,.90)*(white*1.2+exp2(-r*r*105)*flash*.54);
    }
    if(fire>.001)
    {
        float grow=1-exp(-age*4.5);
        float a=atan2(p.y,p.x);
        float2 q=p/(.33+grow*.32);
        q+=float2(11*seed,-age*2.8);
        float n=energyNoise(q*3.8+float2(age*2,0));
        n=.59*n+.28*energyNoise(q*8.4+n)+.13*energyNoise(q*18.0-age);
        float tongues=.105*sin(a*5+seed*17-age*4)+.061*sin(a*9+n*4);
        float edge=.15+grow*.58+tongues+(n-.52)*.32;
        float body=(1-smoothstep(edge-.07,edge+.024,r))*smoothstep(.18,.43,n);
        float rim=exp2(-abs(r-edge+.018)*35)*smoothstep(.26,.84,n);
        float purple=smoothstep(.02,.30,tint.b-tint.g);
        float3 cool=lerp(float3(.66,.055,.006),float3(.27,.06,.87),purple);
        float3 bright=lerp(float3(1,.64,.13),float3(.84,.37,1),purple);
        float3 flame=lerp(cool,bright,smoothstep(.25,.74,n));
        flame=lerp(flame,float3(1,.91,.67),exp2(-r*r*11)*smoothstep(.35,.8,n)*.85);
        float breakup=1-smoothstep(.44,.86,age)*smoothstep(.35,.72,r);
        energy+=(body*flame*.95+rim*bright*.61)*breakup*fire;
    }
    if(wave>.001)
    {
        float radius=.16+.77*(1-exp(-max(0,age-.065)*3.2));
        float angle=atan2(p.y,p.x);
        float d=r-radius-sin(angle*7+seed*6)*.014;
        float front=exp2(-abs(d)*66);
        float wake=exp2(-abs(d+.07)*24)*.30;
        float streak=energyStreams(p,-age*1.2,seed+.53,wave,29);
        energy+=(front*.74+wake+streak*.78)*wave*lerp(tint,float3(1,.83,.43),.43);
    }
    return energy*(1-smoothstep(.84,1,r));
}
#endif
