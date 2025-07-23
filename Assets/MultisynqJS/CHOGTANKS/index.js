import { BUILD_IDENTIFIER } from './buildIdentifier'
import { StartSession }     from '@multisynq/unity-js'
// Choice A should auto-select
// ==== Choice A: ==== If you are using any JsPlugins like [SynqVar] or [SynqRPC]
 import { PluginsModelRoot as _ModelRoot, PluginsViewRoot as _ViewRoot } from './plugins/indexOfPlugins'

// ==== Choice B: ==== If you want to use the default base classes
// import { GameModelRoot as _ModelRoot, GameViewRoot as _ViewRoot } from '@multisynq/unity-js'

//=== ||||||||||| =================================== ||||||| ||||||  ========
class MyModelRoot extends _ModelRoot { // Learn about Croquet Models: https://croquet.io/dev/docs/croquet/Model.html
  init(options) {
    // @ts-ignore-error: init() missing
    super.init(options)
  }
}
// @ts-expect-error: register() missing
MyModelRoot.register('MyModelRoot')

//=== |||||||||| ================================== ||||||| |||||  ========
class MyViewRoot extends _ViewRoot { // Learn about Croquet Views: https://croquet.io/dev/docs/croquet/View.html
  constructor(model) { // calling StartSession() will pass an instance of the model above to tie them together
    super(model)
  }
}

//============ ||||||| ||||||||  ========
// Learn about Croquet Sessions: https://croquet.io/dev/docs/croquet/Session.html
StartSession(MyModelRoot, MyViewRoot, BUILD_IDENTIFIER)

      
      