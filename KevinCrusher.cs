using Celeste;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace KevinCrusher {
    // 1. 告诉游戏和地图编辑器，这个实体的名字叫 KevinCrusher
    [CustomEntity("KevinCrusherMod/KevinCrusher")]
    public class KevinCrusher : Solid { // 继承 Solid，说明它是一个有碰撞箱的固体
        
        // 2. 构造函数：当实体在地图上被创建时执行
        public KevinCrusher(EntityData data, Vector2 offset) 
            : base(data.Position + offset, data.Width, data.Height, safe: true) {
            
            // 这里我们先借用原版 Kevin 的图片贴图，这样游戏里能看得到
            // 以后你可以换成你自己的图片
            Add(new Sprite(GFX.Game, "objects/crushblock/block")); 
        }

        // 3. 大脑函数：游戏运行时，每一帧都会执行这里面的代码
        public override void Update() {
            base.Update(); // 别删这行，它负责维持实体的基本动作

            // 目前这里是空的，它什么都不会做
        }
    }
}