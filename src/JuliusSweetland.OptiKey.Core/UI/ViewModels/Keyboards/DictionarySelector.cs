// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using System;
using JuliusSweetland.OptiKey.UI.ViewModels.Keyboards.Base;

namespace JuliusSweetland.OptiKey.UI.ViewModels.Keyboards
{
    public class DictionarySelector : BackActionKeyboard
    {
        private int pageIndex;

        public DictionarySelector(Action backAction, int pageIndex)
            : base(backAction)
        {
            this.pageIndex = pageIndex;
        }

        public int PageIndex
        {
            get { return pageIndex; }
        }
    }
}
