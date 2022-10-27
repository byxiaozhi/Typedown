import React, { useRef } from 'react';
import { useCallback } from 'react';
import { Scrollbars as ReactCustomScrollbars } from 'react-custom-scrollbars';
import styles from './index.module.scss';
import classNames from 'classnames';

const Scrollbars: React.FC<any> = ({ children, scrollbarRef, ...props }) => {

  const _scrollbarRef = useRef<any>();

  const relativeScrollY = useCallback((delta: number) => {
    const scrollbar = _scrollbarRef.current
    scrollbar.scrollTop(scrollbar.getScrollTop() + delta)
  }, [])

  const relativeScrollX = useCallback((delta: number) => {
    const scrollbar = _scrollbarRef.current
    scrollbar.scrollLeft(scrollbar.getScrollLeft() + delta)
  }, [])

  const onVerticalWheel = useCallback((event: React.WheelEvent) => {
    relativeScrollY(event.deltaY);
  }, [relativeScrollY])

  const onHorizontalWheel = useCallback((event: React.WheelEvent) => {
    relativeScrollX(event.deltaY);
  }, [relativeScrollX])

  const renderTrackVertical = useCallback(
    ({ ...trackProps }: any) => (
      <div {...trackProps} className={classNames(trackProps.className, styles.TrackVertical)} onWheel={onVerticalWheel} />
    ),
    [onVerticalWheel],
  );

  const renderThumbVertical = useCallback(
    ({ ...thumbProps }: any) => (
      <div {...thumbProps} className={classNames(thumbProps.className, styles.ThumbVertical)} />
    ),
    [],
  );

  const renderTrackHorizontal = useCallback(
    ({ ...trackProps }: any) => (
      <div {...trackProps} className={classNames(trackProps.className, styles.TrackHorizontal)} onWheel={onHorizontalWheel} />
    ),
    [onHorizontalWheel],
  );

  const renderThumbHorizontal = useCallback(
    ({ ...thumbProps }: any) => (
      <div {...thumbProps} className={classNames(thumbProps.className, styles.ThumbHorizontal)} />
    ),
    [],
  );

  return (
    <ReactCustomScrollbars
      renderThumbHorizontal={renderThumbHorizontal}
      renderThumbVertical={renderThumbVertical}
      renderTrackHorizontal={renderTrackHorizontal}
      renderTrackVertical={renderTrackVertical}
      hideTracksWhenNotNeeded={true}
      ref={(e) => {
        _scrollbarRef.current = e;
        scrollbarRef?.(e)
      }}
      {...props}
    >
      {children}
    </ReactCustomScrollbars>
  );
};

export default Scrollbars;