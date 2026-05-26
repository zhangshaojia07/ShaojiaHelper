using Celeste;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.KevinCrusher.Entities {

    [CustomEntity("KevinCrusher/KevinCrusher")]
    [TrackedAs(typeof(CrushBlock))]
    public class KevinCrusher : Solid { // 继承 Solid
        
        // 构造函数：当实体在地图上被创建时执行
        public KevinCrusher(EntityData data, Vector2 offset) 
            : base(data.Position + offset, data.Width, data.Height, safe: true) {
            
        }

        // 游戏运行时，每一帧都会执行这里面的代码
        public override void Update() {
            base.Update(); // 别删这行，它负责维持实体的基本动作
        }

        public override void Render() {
            base.Render();
            
            // 在实体的坐标（Position），画一个和地图配置一样宽（Width）高（Height）的红方块
            Draw.Rect(Position, Width, Height, Color.Red);
        }
    }
}