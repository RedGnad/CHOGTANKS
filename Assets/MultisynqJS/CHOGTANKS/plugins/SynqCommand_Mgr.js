import { Model } from '@croquet/croquet';

export class SynqCommand_Mgr_Model extends Model {
  dbg = false
  init(options) {
    super.init(options);
    this.subscribe('SynqCommand', 'pleaseRun', this.onPleaseRun);
    if (this.dbg) console.log('### <color=magenta>SynqCommand_Mgr_Model.init() <<<<<<<<<<<<<<<<<<<<< </color>');
  }
  onPleaseRun(msg) {
    if (this.dbg) console.log(`<color=blue>[SynqCommand]</color> <color=yellow>JS</color> CroquetModel <color=magenta>SynqCommandMgrModel.onSynqCommandExecute()</color> msg = <color=white>${JSON.stringify(msg)}</color>`);
    this.publish('SynqCommand', 'everybodyRun', msg);
  }
}
SynqCommand_Mgr_Model.register('SynqCommand_Mgr_Model');
        