import{r as l,f as m}from"./vendor-BeY2h1CR.js";var u={exports:{}},n={};/**
 * @license React
 * react-jsx-runtime.production.min.js
 *
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the MIT license found in the
 * LICENSE file in the root directory of this source tree.
 */var c=l,y=Symbol.for("react.element"),x=Symbol.for("react.fragment"),R=Object.prototype.hasOwnProperty,v=c.__SECRET_INTERNALS_DO_NOT_USE_OR_YOU_WILL_BE_FIRED.ReactCurrentOwner,d={key:!0,ref:!0,__self:!0,__source:!0};function i(t,r,p){var e,o={},s=null,a=null;p!==void 0&&(s=""+p),r.key!==void 0&&(s=""+r.key),r.ref!==void 0&&(a=r.ref);for(e in r)R.call(r,e)&&!d.hasOwnProperty(e)&&(o[e]=r[e]);if(t&&t.defaultProps)for(e in r=t.defaultProps,r)o[e]===void 0&&(o[e]=r[e]);return{$$typeof:y,type:t,key:s,ref:a,props:o,_owner:v.current}}n.Fragment=x;n.jsx=i;n.jsxs=i;u.exports=n;var O=u.exports,f={},_=m;f.createRoot=_.createRoot,f.hydrateRoot=_.hydrateRoot;export{f as c,O as j};
