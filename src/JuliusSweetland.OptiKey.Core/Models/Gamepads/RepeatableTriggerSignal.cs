<<<<<<< HEAD
﻿using System;
=======
﻿﻿using System;
>>>>>>> master-keyrepeats
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JuliusSweetland.OptiKey.Models.Gamepads
{
<<<<<<< HEAD
    public struct RepeatableTriggerSignal 
=======
    public struct RepeatableTriggerSignal
>>>>>>> master-keyrepeats
    {
        public RepeatableTriggerSignal(TriggerSignal triggerSignal, bool isRepeat)
        {
            this.triggerSignal = triggerSignal;
            this.isRepeat = isRepeat;
            this.isRepeatAllowed = false; // to be updated when context is known
        }

        public TriggerSignal triggerSignal;
        public bool isRepeat;
        public bool isRepeatAllowed;
    }
}
