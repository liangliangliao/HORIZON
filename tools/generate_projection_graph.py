"""Deterministically build HORIZON's editable URP Unlit Shader Graph.

The graph connects exposed material properties and UVs to the projection function,
then connects its RGB/alpha outputs to the URP master stack. No external assets.
"""
import hashlib
import json
from pathlib import Path

objects = []
nodes = []
edges = []
properties = []


def uid(name):
    return hashlib.md5(('HORIZON projection ' + name).encode()).hexdigest()


def ref(name):
    return {'m_Id': uid(name)}


def obj(name, kind, version=0, **fields):
    value = dict(m_SGVersion=version, m_Type=kind, m_ObjectId=uid(name), **fields)
    objects.append(value)
    return value


def slot(owner, index, name, kind='Vector1', value=0, output=False, stage=3, **extra):
    key = f'{owner} slot {index}'
    obj(key, 'UnityEditor.ShaderGraph.' + kind + 'MaterialSlot', m_Id=index,
        m_DisplayName=name, m_SlotType=int(output), m_Hidden=False,
        m_ShaderOutputName=name.replace(' ', ''), m_StageCapability=stage,
        m_Value=value, m_DefaultValue=value, m_Labels=[], **extra)
    return ref(key)


def node(name, kind, slots, version=0, **extra):
    nodes.append(ref(name))
    return obj(name, 'UnityEditor.ShaderGraph.' + kind, version,
               m_Group={'m_Id': ''}, m_Name=name, m_DrawState={'m_Expanded': True,
               'm_Position': {'serializedVersion': '2', 'x': -700 + len(nodes)*110,
                              'y': len(nodes)*40, 'width': 180, 'height': 140}},
               m_Slots=slots, synonyms=[], m_Precision=0, m_PreviewExpanded=True,
               m_PreviewMode=0, m_CustomColors={'m_SerializableColors': []}, **extra)


def edge(source, source_slot, target, target_slot):
    edges.append({'m_OutputSlot': {'m_Node': ref(source), 'm_SlotId': source_slot},
                  'm_InputSlot': {'m_Node': ref(target), 'm_SlotId': target_slot}})


zero3 = dict(x=0, y=0, z=0)
vertex_blocks = []
for name in ('Position', 'Normal', 'Tangent'):
    block = 'VertexDescription.' + name
    node(block, 'BlockNode', [slot(block, 0, name, name, zero3, stage=1, m_Space=0)],
         m_SerializedDescriptor=block)
    vertex_blocks.append(ref(block))

for name, default, color in [('BaseColor', dict(r=.24, g=.9, b=1, a=.8), True),
                              ('Reveal', 1, False), ('Clock', 0, False)]:
    prop = 'Property ' + name
    properties.append(ref(prop))
    obj(prop, 'UnityEditor.ShaderGraph.Internal.' + ('Color' if color else 'Vector1') + 'ShaderProperty',
        3 if color else 1, m_Guid={'m_GuidSerialized': uid(prop)}, m_Name=name,
        m_DefaultRefNameVersion=1, m_RefNameGeneratedByDisplayName=name,
        m_DefaultReferenceName='_' + name, m_OverrideReferenceName='_' + name,
        m_GeneratePropertyBlock=True, m_UseCustomSlotLabel=False, m_CustomSlotLabel='',
        m_Precision=0, overrideHLSLDeclaration=False, hlslDeclarationOverride=0,
        m_Hidden=False, m_Value=default, m_ColorMode=0, m_FloatType=0)
    node(name, 'PropertyNode', [slot(name, 0, 'Out', 'Vector4' if color else 'Vector1',
        dict(x=.24, y=.9, z=1, w=.8) if color else default, output=True)], m_Property=ref(prop))

node('UV', 'UVNode', [slot('UV', 0, 'Out', 'Vector4', dict(x=0,y=0,z=0,w=0), output=True)], m_OutputChannel=0)
function = 'Projection scan and dissolve'
node(function, 'CustomFunctionNode', [
    slot(function,0,'UV','Vector2',dict(x=0,y=0)),
    slot(function,1,'Tint','Vector4',dict(x=.24,y=.9,z=1,w=.8)),
    slot(function,2,'Reveal',value=1), slot(function,3,'Clock'),
    slot(function,4,'RGB','Vector3',zero3,output=True), slot(function,5,'Alpha',output=True)],
    version=1, m_SourceType=1, m_FunctionName='HorizonProjection', m_FunctionSource='',
    m_FunctionBody='''float scan = .72 + .28 * step(.82, frac(UV.y * 36 - Clock * .9));
float grain = frac(sin(dot(floor(UV * 80), float2(12.9898,78.233))) * 43758.5453);
float visible = step(grain, saturate(Reveal) * 1.06 - .03);
float edge = 1 - smoothstep(0,.08,abs(grain-Reveal));
RGB = Tint.rgb * scan + edge * (1-Reveal) * float3(1,.65,.2);
Alpha = Tint.a * visible * saturate(Reveal*5) * scan;''')
for prop, i in [('BaseColor',1),('Reveal',2),('Clock',3)]: edge(prop,0,function,i)
edge('UV',0,function,0)
fragments=[]
for name, kind, value, output_slot in [('BaseColor','Vector3',zero3,4),('Alpha','Vector1',1,5)]:
    block='SurfaceDescription.'+name
    node(block,'BlockNode',[slot(block,0,name,kind,value,stage=2)],m_SerializedDescriptor=block)
    edge(function,output_slot,block,0); fragments.append(ref(block))
obj('Unlit','UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget',2)
obj('URP','UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget',1,
    m_Datas=[],m_ActiveSubTarget=ref('Unlit'),m_AllowMaterialOverride=False,
    m_SurfaceType=1,m_ZTestMode=4,m_ZWriteControl=2,m_AlphaMode=0,m_RenderFace=0,
    m_AlphaClip=False,m_CastShadows=False,m_ReceiveShadows=False,m_DisableTint=False,
    m_AdditionalMotionVectorMode=0,m_AlembicMotionVectors=False,m_SupportsLODCrossFade=False,
    m_CustomEditorGUI='',m_SupportVFX=False)
obj('Category','UnityEditor.ShaderGraph.CategoryData',m_Name='',m_ChildObjectList=properties)
graph=obj('Graph','UnityEditor.ShaderGraph.GraphData',3,
    m_Properties=properties,m_Keywords=[],m_Dropdowns=[],m_CategoryData=[ref('Category')],
    m_Nodes=nodes,m_GroupDatas=[],m_StickyNoteDatas=[],m_Edges=edges,
    m_VertexContext={'m_Position':dict(x=600,y=0),'m_Blocks':vertex_blocks},
    m_FragmentContext={'m_Position':dict(x=600,y=300),'m_Blocks':fragments},
    m_PreviewData={'serializedMesh':{'m_SerializedMesh':'{"mesh":{"instanceID":0}}','m_Guid':''},'preventRotation':False},
    m_Path='HORIZON',m_GraphPrecision=1,m_PreviewMode=2,m_OutputNode={'m_Id':''},
    m_SubDatas=[],m_ActiveTargets=[ref('URP')])
path=Path(__file__).resolve().parents[1]/'Assets/Resources/HorizonProjection.shadergraph'
path.write_text('\n\n'.join(json.dumps(x,indent=4) for x in [graph]+objects[:-1])+'\n')
