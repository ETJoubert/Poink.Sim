namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// JS for a Profiler chart row's draggable dividers: resetEqual splits the visible panels evenly, bind lets a divider
	/// move width between its two neighbours. Panels are sized by flex-basis percent so any panel count always sums to 100%.
	/// </summary>
	internal static class ProfilerChartDividerScript
	{
		public const string Source = """
			(function() {
			    // Every .prof-chart-main/.prof-chart-side child gets an explicit flex-basis
			    // percent (not width px) so N panels always sum to 100% regardless of window
			    // size -- resetEqual splits it evenly, bind's drag only ever moves percent
			    // between the divider's own two immediate neighbours.
			    // Net px a divider adds beyond its neighbours' own width: two 16px gaps narrowed
			    // by its -10px margins, plus its own 6px -- keep in sync with .prof-chart's CSS.
			    var DIVIDER_CHROME_PX = 18;
			    function resetEqual(container) {
			        if (!container) return;
			        var panels = container.querySelectorAll(':scope > .prof-chart-main, :scope > .prof-chart-side');
			        if (panels.length === 0) return;
			        var dividerCount = container.querySelectorAll(':scope > .prof-chart-divider').length;
			        var style = getComputedStyle(container);
			        var containerWidth = container.getBoundingClientRect().width
			            - (parseFloat(style.paddingLeft) || 0)
			            - (parseFloat(style.paddingRight) || 0);
			        var reservedPercent = containerWidth > 0
			            ? (dividerCount * DIVIDER_CHROME_PX / containerWidth) * 100 : 0;
			        var percent = (100 - reservedPercent) / panels.length;
			        panels.forEach(function(p) { p.style.flex = '0 0 ' + percent + '%'; });
			    }
			    function bind(divider) {
			        if (!divider || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minWidthPx = parseInt(divider.dataset.minWidth, 10) || 200;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var left = divider.previousElementSibling;
			            var right = divider.nextElementSibling;
			            if (!left || !right) return;
			            var container = divider.parentElement;
			            // flex-basis % resolves against the content box, not the border box
			            // getBoundingClientRect returns -- subtract padding or drags drift right-ward.
			            var containerStyle = getComputedStyle(container);
			            var containerWidth = container.getBoundingClientRect().width
			                - (parseFloat(containerStyle.paddingLeft) || 0)
			                - (parseFloat(containerStyle.paddingRight) || 0);
			            var minPercent = (minWidthPx / containerWidth) * 100;
			            var startX = e.clientX;
			            var startLeftPercent = (left.getBoundingClientRect().width / containerWidth) * 100;
			            var startRightPercent = (right.getBoundingClientRect().width / containerWidth) * 100;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('prof-chart-divider-active');

			            function onMove(ev) {
			                var deltaPercent = ((ev.clientX - startX) / containerWidth) * 100;
			                var newLeft = startLeftPercent + deltaPercent;
			                var newRight = startRightPercent - deltaPercent;
			                if (newLeft < minPercent) { newRight -= (minPercent - newLeft); newLeft = minPercent; }
			                if (newRight < minPercent) { newLeft -= (minPercent - newRight); newRight = minPercent; }
			                left.style.flex = '0 0 ' + newLeft + '%';
			                right.style.flex = '0 0 ' + newRight + '%';
			            }
			            function onUp(ev) {
			                divider.releasePointerCapture(e.pointerId);
			                divider.removeEventListener('pointermove', onMove);
			                divider.removeEventListener('pointerup', onUp);
			                divider.classList.remove('prof-chart-divider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    return { bind: bind, resetEqual: resetEqual };
			})()
			""";
	}
}
