import{r as b,g as x}from"./vendor-Dfky3cn9.js";function h(e){var r,t,u="";if(typeof e=="string"||typeof e=="number")u+=e;else if(typeof e=="object")if(Array.isArray(e)){var n=e.length;for(r=0;r<n;r++)e[r]&&(t=h(e[r]))&&(u&&(u+=" "),u+=t)}else for(t in e)e[t]&&(u&&(u+=" "),u+=t);return u}function N(){for(var e,r,t=0,u="",n=arguments.length;t<n;t++)(e=arguments[t])&&(r=h(e))&&(u&&(u+=" "),u+=r);return u}var g={exports:{}},w={},$={exports:{}},j={};/**
 * @license React
 * use-sync-external-store-shim.production.js
 *
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var s=b;function V(e,r){return e===r&&(e!==0||1/e===1/r)||e!==e&&r!==r}var D=typeof Object.is=="function"?Object.is:V,R=s.useState,O=s.useEffect,z=s.useLayoutEffect,I=s.useDebugValue;function M(e,r){var t=r(),u=R({inst:{value:t,getSnapshot:r}}),n=u[0].inst,o=u[1];return z(function(){n.value=t,n.getSnapshot=r,d(n)&&o({inst:n})},[e,t,r]),O(function(){return d(n)&&o({inst:n}),e(function(){d(n)&&o({inst:n})})},[e]),I(t),t}function d(e){var r=e.getSnapshot;e=e.value;try{var t=r();return!D(e,t)}catch{return!0}}function _(e,r){return r()}var A=typeof window>"u"||typeof window.document>"u"||typeof window.document.createElement>"u"?_:M;j.useSyncExternalStore=s.useSyncExternalStore!==void 0?s.useSyncExternalStore:A;$.exports=j;var C=$.exports;/**
 * @license React
 * use-sync-external-store-shim/with-selector.production.js
 *
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var m=b,G=C;function L(e,r){return e===r&&(e!==0||1/e===1/r)||e!==e&&r!==r}var k=typeof Object.is=="function"?Object.is:L,F=G.useSyncExternalStore,U=m.useRef,W=m.useEffect,B=m.useMemo,H=m.useDebugValue;w.useSyncExternalStoreWithSelector=function(e,r,t,u,n){var o=U(null);if(o.current===null){var f={hasValue:!1,value:null};o.current=f}else f=o.current;o=B(function(){function y(a){if(!E){if(E=!0,l=a,a=u(a),n!==void 0&&f.hasValue){var c=f.value;if(n(c,a))return v=c}return v=a}if(c=v,k(l,a))return c;var p=u(a);return n!==void 0&&n(c,p)?(l=a,c):(l=a,v=p)}var E=!1,l,v,S=t===void 0?null:t;return[function(){return y(r())},S===null?void 0:function(){return y(S())}]},[r,t,u,n]);var i=F(e,o[0],o[1]);return W(function(){f.hasValue=!0,f.value=i},[i]),H(i),i};g.exports=w;var J=g.exports;const P=x(J);export{N as c,P as u};
