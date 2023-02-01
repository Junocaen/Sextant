// Copyright (c) 2021 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using DynamicData;
using DynamicData.Aggregation;
using DynamicData.Binding;
using ReactiveUI;
using Splat;

namespace Sextant.Avalonia
{
    /// <summary>
    /// A view used within navigation in Sextant with Avalonia.
    /// </summary>
    public sealed partial class NavigationView
    {
        /// <summary>
        /// This class represents a single navigation layer.
        /// </summary>
        private class Navigation : IDisposable, IEnableLogger
        {
            private readonly SourceList<IViewFor> _navigationStack = new(); // new ObservableCollection<IViewFor>();
            private readonly IPageTransition? _transition;
            private readonly Subject<IViewModel> _pagePoppedSubject = new();
            private CompositeDisposable _disposables = new();
            private readonly IFullLogger _logger;

            /// <summary>
            /// Initializes a new instance of the <see cref="Navigation"/> class.
            /// </summary>
            public Navigation()
            {
                _logger = this.Log();
                var navigationStackObservable = _navigationStack
                    .Connect()
                    .OnItemRemoved(view =>
                    {
                        var viewmodel = (IViewModel)view.ViewModel;
                        _pagePoppedSubject.OnNext(viewmodel);
                        _logger.Debug($"Junocaen log: popped viewmodel {viewmodel.Id}");
                    })
                    .DisposeMany()
                    .Publish()
                    .RefCount();

                void SetContent(IReadOnlyCollection<IViewFor> pages)
                {
                    var lastPage = pages.Last();
                    var viewmodel = lastPage.ViewModel as IViewModel;
                    _logger.Debug($"Junocaen log: new VM set to '{viewmodel?.Id}'");
                    Control.Content = pages.Last();
                }

                navigationStackObservable
                    .ToCollection()
                    .Subscribe(SetContent)
                    .DisposeWith(_disposables);

                CountChanged = navigationStackObservable.Count();
                _transition = new PageSlide(TimeSpan.FromMilliseconds(50));
            }

            public IObservable<IViewModel> PagePopped => _pagePoppedSubject.AsObservable();

            /// <summary>
            /// Gets a value indicating whether the control is visible.
            /// </summary>
            public bool IsVisible => _navigationStack.Count > 1;

            /// <summary>
            /// Gets a the page count.
            /// </summary>
            public IObservable<int> CountChanged { get; }

            /// <summary>
            /// Gets the control responsible for rendering the current view.
            /// </summary>
            // TransitioningContentControl is unstable with backward navigation; 
            // it does not always make the TransitioningContentControl.Control visable
            // thus use ContentControl in the meanwhile. Do not use animations anyway :)
            // Github issue. https://github.com/AvaloniaUI/Avalonia/issues/10108#issue-1560581929
            // fix will be published soon.
            public IContentControl Control { get; } = new ContentControl(); //new TransitioningContentControl();

            /// <summary>
            /// Toggles the animations.
            /// </summary>
            /// <param name="enable">Returns true if we are enabling the animations.</param>
            public void ToggleAnimations(bool enable)
            {
                if (Control is TransitioningContentControl control)
                {
                    control.Content =
                        enable
                            ? _transition
                            : null;
                }
            }

            /// <summary>
            /// Adds a <see cref="IViewFor"/> to the navigation stack.
            /// </summary>
            /// <param name="view">The view to add to the navigation stack.</param>
            /// <param name="resetStack">Defines if we should reset the navigation stack.</param>
            public void Push(IViewFor view, bool resetStack = false) =>
                _navigationStack.Edit(views =>
                {
                    if (resetStack)
                    {
                        views.Clear();
                    }

                    views.Add(view);
                });

            /// <summary>
            /// Removes the last <see cref="IViewFor"/> from the navigation stack.
            /// </summary>
            public void Pop() => _navigationStack.RemoveAt(_navigationStack.Count - 1);

            /// <summary>
            /// Removes all pages except the first one <see cref="IViewFor"/> from the navigation stack.
            /// </summary>
            public void PopToRoot() =>
                _navigationStack.Edit(views =>
                {
                    var rootView = views.First();
                    views.Clear();
                    views.Add(rootView);
                });

            public void Dispose()
            {
                _navigationStack.Dispose();
                _pagePoppedSubject.Dispose();
                _disposables.Dispose();
            }
        }
    }
}
